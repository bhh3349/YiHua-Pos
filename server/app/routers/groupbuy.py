"""groupbuy —— 团购域路由（T-405）。

**本文件不含任何平台细节**：所有平台差异都藏在 ``PlatformAdapter`` 后面
（任务卡 4.6 硬要求）。要加新平台，新增一个 adapter 文件即可，本文件零改动
—— 这一点由 ``tests/test_groupbuy_platform.py`` 证明。

金额约定：请求里的金额收成字符串/数字后立刻转 Decimal；响应里的金额
一律输出字符串（如 ``"99.00"``），避免 JSON float 精度丢失。
"""
import os
from datetime import datetime
from typing import List, Optional

from fastapi import APIRouter, Depends, Request
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..config import DATA_DIR
from ..db import get_db
from ..models import Order
from ..models_groupbuy import COUPON_UNUSED, GroupbuyCoupon, GroupbuyRedeemLog
from ..util import BizError, Ctx, get_ctx, ok, oss_dir_for, oss_url, resolve_oss_base
from ..services.groupbuy import guard, reconciliation, store as cfg_store
from ..services.groupbuy.errors import (COUPON_ERROR_MESSAGES, GroupbuyError,
                                        PLATFORM_ERROR_MESSAGES, PlatformCode)
from ..services.groupbuy.platform import (CouponQuery, get_adapter,
                                          registered_platforms)
from ..services.groupbuy.verify import COUPON_CODE_RE

router = APIRouter(prefix="/api/v1/groupbuy", tags=["groupbuy"])


# ==================== 业务异常 -> 标准响应 ====================
# GroupbuyError 在各接口内 try/except 捕获后用 _fail 转成 BizError，
# 由 main.py 的全局 BizError 处理器输出标准响应体（code = 细分错误码）。


def _fail(e: GroupbuyError):
    """把团购域异常转成全局处理器认识的 BizError。"""
    raise BizError(e.code, e.message, e.data)


# ==================== 平台配置 ====================
class ConfigSaveReq(BaseModel):
    platformCode: str
    storeIdList: List[int] = []
    signKey: str = ""                 # 不传/传空表示不修改已存密钥
    appId: str = ""
    remark: str = ""
    status: int = 1


@router.post("/platformList")
def platform_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    """平台清单：已注册的 adapter + 中文名 + 配置状态。"""
    from ..services.groupbuy.errors import PLATFORM_NAMES
    configured = {c["platformCode"]: c for c in cfg_store.list_configs(db, ctx.tenant_id)}
    items = []
    for code in registered_platforms():
        try:
            pc = PlatformCode(code)
            name = PLATFORM_NAMES[pc]
        except ValueError:
            name = code
        c = configured.get(code)
        items.append({
            "platformCode": code, "platformName": name,
            "configured": bool(c), "enabled": bool(c and c["status"]),
            "storeIdList": (c or {}).get("storeIdList") or [],
        })
    return ok(items)


@router.post("/configList")
def config_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    return ok(cfg_store.list_configs(db, ctx.tenant_id))


@router.post("/configSave")
def config_save(req: ConfigSaveReq, ctx: Ctx = Depends(get_ctx),
                db: Session = Depends(get_db)):
    """新增/更新平台配置。密钥加密落库；响应里只回掩码。"""
    if not req.storeIdList:
        raise BizError("400", "storeIdList 不能为空（至少绑定一个门店）")
    try:
        row = cfg_store.upsert_config(db, ctx.tenant_id, req.platformCode,
                                      req.storeIdList, req.signKey,
                                      app_id=req.appId, remark=req.remark,
                                      status=req.status)
    except GroupbuyError as e:
        _fail(e)
    return ok(cfg_store.config_dto(row))


class ConfigStatusReq(BaseModel):
    platformCode: str
    status: int


@router.post("/configStatus")
def config_status(req: ConfigStatusReq, ctx: Ctx = Depends(get_ctx),
                  db: Session = Depends(get_db)):
    try:
        row = cfg_store.set_config_status(db, ctx.tenant_id, req.platformCode, req.status)
    except GroupbuyError as e:
        _fail(e)
    return ok(cfg_store.config_dto(row))


# ==================== 验券 ====================
class VerifyReq(BaseModel):
    platformCode: str
    couponCode: str
    storeId: int = 0
    booked: bool = False            # 已完成预约（有预约限制的券要用）


@router.post("/coupon/verify")
def coupon_verify(req: VerifyReq, ctx: Ctx = Depends(get_ctx),
                  db: Session = Depends(get_db)):
    """验券（不改状态，可反复调）。失败细分到 8 种券状态错误码。"""
    try:
        guard.check_rate_limit("verify:%s" % ctx.store_id)
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        cfg_store.check_store_allowed(cfg, req.storeId or ctx.store_id)
        q = cfg_store.build_query(db, ctx.tenant_id, req.storeId or ctx.store_id,
                                  req.platformCode, req.couponCode, booked=req.booked)
        result = get_adapter(req.platformCode).verify_coupon(q)
    except GroupbuyError as e:
        _fail(e)
    if not result.valid:
        return ok({"valid": False, "errorCode": result.error_code,
                   "message": result.message, "coupon": None}, message=result.message)
    return ok({"valid": True, "coupon": result.coupon.to_dict()})


# ==================== 核销 ====================
class RedeemReq(BaseModel):
    platformCode: str
    couponCode: str
    storeId: int = 0
    orderId: int = 0                # 关联的本地订单（方式 A）；方式 B 可为 0
    actualDiscount: Optional[str] = None   # 实际抵扣金额；空 = 按券面抵扣额
    booked: bool = False


@router.post("/coupon/redeem")
def coupon_redeem(req: RedeemReq, ctx: Ctx = Depends(get_ctx),
                  db: Session = Depends(get_db)):
    """核销。幂等：同一张券只能成功一次（并发下由条件更新保证）。"""
    store_id = req.storeId or ctx.store_id
    try:
        guard.check_rate_limit("redeem:%s" % ctx.store_id)
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        cfg_store.check_store_allowed(cfg, store_id)
        if req.orderId:
            _check_order(db, ctx, req.orderId)
        q = cfg_store.build_query(db, ctx.tenant_id, store_id, req.platformCode,
                                  req.couponCode, order_id=req.orderId,
                                  actual_discount=req.actualDiscount,
                                  operator_id=ctx.user_id, booked=req.booked)
        result = get_adapter(req.platformCode).redeem_coupon(q)
    except GroupbuyError as e:
        _fail(e)
    if not result.success:
        _fail(GroupbuyError(result.error_code, result.message))
    return ok(_redeem_dto(result))


def _check_order(db: Session, ctx: Ctx, order_id: int):
    """核销关联的订单必须存在且属于本门店。"""
    o = db.get(Order, order_id)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    return o


def _redeem_dto(result) -> dict:
    d = {"success": True,
         "coupon": result.coupon.to_dict() if result.coupon else None,
         "actualDiscount": str(result.actual_discount)}
    return d


# ==================== 撤销核销 ====================
class ReverseReq(BaseModel):
    platformCode: str
    couponCode: str
    storeId: int = 0
    reason: str                     # 撤销原因（必填，落流水）


@router.post("/coupon/reverse")
def coupon_reverse(req: ReverseReq, ctx: Ctx = Depends(get_ctx),
                   db: Session = Depends(get_db)):
    """撤销核销（误操作）。撤销后券回到待核销，可重新核销。"""
    try:
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        cfg_store.check_store_allowed(cfg, req.storeId or ctx.store_id)
        q = cfg_store.build_query(db, ctx.tenant_id, req.storeId or ctx.store_id,
                                  req.platformCode, req.couponCode,
                                  operator_id=ctx.user_id, reason=req.reason)
        result = get_adapter(req.platformCode).reverse_redeem(q)
    except GroupbuyError as e:
        _fail(e)
    if not result.success:
        _fail(GroupbuyError(result.error_code, result.message))
    return ok({"success": True, "coupon": result.coupon.to_dict() if result.coupon else None})


# ==================== 核销记录查询 ====================
class LogQueryReq(BaseModel):
    startDate: str = ""             # YYYY-MM-DD
    endDate: str = ""
    platformCode: str = ""
    couponCode: str = ""
    pageNumber: int = 1
    pageSize: int = 20


@router.post("/redeemLog")
def redeem_log(req: LogQueryReq, ctx: Ctx = Depends(get_ctx),
               db: Session = Depends(get_db)):
    """核销流水：谁、什么时候、哪个订单、抵了多少（含撤销记录）。"""
    q = (db.query(GroupbuyRedeemLog)
           .filter(GroupbuyRedeemLog.tenant_id == ctx.tenant_id,
                   GroupbuyRedeemLog.store_id == ctx.store_id))
    if req.startDate:
        q = q.filter(GroupbuyRedeemLog.created_at >= req.startDate)
    if req.endDate:
        q = q.filter(GroupbuyRedeemLog.created_at <= req.endDate + " 23:59:59")
    if req.platformCode:
        q = q.filter(GroupbuyRedeemLog.platform_code == req.platformCode)
    if req.couponCode:
        q = q.filter(GroupbuyRedeemLog.coupon_code == req.couponCode.strip())
    total = q.count()
    rows = (q.order_by(GroupbuyRedeemLog.id.desc())
             .offset((max(req.pageNumber, 1) - 1) * req.pageSize)
             .limit(req.pageSize).all())
    return ok({"total": total, "list": [{
        "id": r.id, "couponCode": r.coupon_code, "platformCode": r.platform_code,
        "orderId": r.order_id, "action": r.action, "reason": r.reason or "",
        "actualDiscount": str(r.actual_discount or 0),
        "operatorId": r.operator_id,
        "createdAt": r.created_at.strftime("%Y-%m-%d %H:%M:%S") if r.created_at else "",
    } for r in rows]})


# ==================== 对账 ====================
class ReconcileReq(BaseModel):
    platformCode: str
    date: str                       # YYYY-MM-DD
    storeId: int = 0


@router.post("/reconcile")
def reconcile(req: ReconcileReq, ctx: Ctx = Depends(get_ctx),
              db: Session = Depends(get_db)):
    """按日/按平台对账：本地核销记录 vs 平台侧记录，返回差异明细。"""
    try:
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        day = datetime.strptime(req.date, "%Y-%m-%d")
        store_id = req.storeId or ctx.store_id
        cfg_store.check_store_allowed(cfg, store_id)
        q = cfg_store.build_query(db, ctx.tenant_id, store_id, req.platformCode, "")
        result = reconciliation.reconcile(db, q, day, store_id=store_id)
    except GroupbuyError as e:
        _fail(e)
    return ok(result)


@router.post("/reconcileExport")
def reconcile_export(req: ReconcileReq, request: Request,
                     ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    """对账结果导出 CSV（UTF-8 BOM，Excel 打开中文不乱码）。"""
    try:
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        day = datetime.strptime(req.date, "%Y-%m-%d")
        store_id = req.storeId or ctx.store_id
        cfg_store.check_store_allowed(cfg, store_id)
        q = cfg_store.build_query(db, ctx.tenant_id, store_id, req.platformCode, "")
        result = reconciliation.reconcile(db, q, day, store_id=store_id)
        base_dir = oss_dir_for(ctx.tenant_id, ctx.store_id, sub="groupbuy")
        fname = "groupbuy_reconcile_%s_%s_%s.csv" % (
            req.platformCode, req.date, datetime.now().strftime("%Y%m%d%H%M%S"))
        path = reconciliation.export_csv(result, os.path.join(base_dir, fname))
    except GroupbuyError as e:
        _fail(e)
    oss_base = resolve_oss_base(request)
    return ok({"url": oss_url(path, oss_base), "summary": result["summary"]})


# ==================== 平台回调（预留 + 自有平台验签示范） ====================
class CallbackReq(BaseModel):
    platformCode: str
    payload: dict = {}
    sign: str = ""


@router.post("/callback")
def callback(req: CallbackReq, ctx: Ctx = Depends(get_ctx),
             db: Session = Depends(get_db)):
    """平台回调入口：先验签，再分发。

    验签失败回 SIGN_INVALID；本期只有自有平台实现了回调验签，
    其余平台走 adapter 骨架的「平台未接入」。**sign_key 不写日志、不回显**。
    """
    try:
        cfg = cfg_store.ensure_platform_enabled(db, ctx.tenant_id, req.platformCode)
        q = cfg_store.build_query(db, ctx.tenant_id, ctx.store_id,
                                  req.platformCode, "")
        adapter = get_adapter(req.platformCode)
        if not adapter.verify_sign(req.payload, req.sign, q.sign_key):
            raise GroupbuyError("SIGN_INVALID")
    except GroupbuyError as e:
        _fail(e)
    # 本期没有需要处理的回调事件；验签通过即确认。
    return ok({"accepted": True})


# ==================== 券发放（自有平台运营用） ====================
class CouponIssueReq(BaseModel):
    couponCode: str
    couponName: str = ""
    couponType: str = "cash"        # package | cash
    faceValue: str = "0.00"
    discountAmount: str = "0.00"
    validFrom: str = ""             # YYYY-MM-DD HH:MM:SS
    validTo: str = ""
    restrictions: dict = {}
    platformCouponId: str = ""


def _parse_dt(s: str) -> Optional[datetime]:
    s = (s or "").strip()
    if not s:
        return None
    return datetime.strptime(s, "%Y-%m-%d %H:%M:%S")


@router.post("/coupon/issue")
def coupon_issue(req: CouponIssueReq, ctx: Ctx = Depends(get_ctx),
                 db: Session = Depends(get_db)):
    """自有平台发券（运营/测试用）。

    发券是自有平台特有的运营动作，不属于 PlatformAdapter 协议，
    所以直接放在这里、不走 adapter。
    """
    code = (req.couponCode or "").strip()
    if not COUPON_CODE_RE.match(code):
        raise BizError("400", "券码格式非法（仅允许 4~64 位字母数字）")
    if req.couponType not in ("package", "cash"):
        raise BizError("400", "couponType 只允许 package | cash")

    exists = (db.query(GroupbuyCoupon)
                .filter(GroupbuyCoupon.tenant_id == ctx.tenant_id,
                        GroupbuyCoupon.coupon_code == code)
                .first())
    if exists:
        raise BizError("409", "券码已存在")

    row = GroupbuyCoupon(
        tenant_id=ctx.tenant_id, platform_code=PlatformCode.SELF,
        coupon_code=code, platform_coupon_id=req.platformCouponId or "",
        coupon_name=req.couponName or "", coupon_type=req.couponType,
        face_value=req.faceValue, discount_amount=req.discountAmount,
        valid_from=_parse_dt(req.validFrom), valid_to=_parse_dt(req.validTo),
        restrictions=req.restrictions or {}, status=COUPON_UNUSED)
    db.add(row)
    db.commit()
    db.refresh(row)
    return ok({"couponId": row.id, "couponCode": row.coupon_code,
               "couponName": row.coupon_name, "status": row.status})
