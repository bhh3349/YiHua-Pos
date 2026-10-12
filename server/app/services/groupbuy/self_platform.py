"""自有平台（self）适配器 —— 本期唯一完整实现的平台。

自有平台没有远端：券就存在本地 ``groupbuy_coupon`` 表里，验券/核销/
撤销/对账都直接读写本地库。对外行为与远端平台 adapter 完全一致
（同样的 DTO、同样的错误码），业务路由感知不到区别。

**并发幂等**（任务卡 4.3，资金安全）：核销用「条件更新」抢占 ——
``UPDATE ... SET status=USED WHERE id=? AND status=UNUSED`` 在 SQLite
和 MySQL 上都是原子操作，两个并发请求只有一个能把状态改掉，
受影响行数为 0 的那个直接失败，绝无可能同一张券成功两次。

**撤销语义**：撤销把核销信息清零（券回到待核销、可重新核销），
同时给该券此前的核销流水打作废标记（``reason=REVERSED``）——
对账口径 = 未作废的核销流水，撤销掉的券两侧都不计入。
"""
import hashlib
import hmac
from datetime import datetime, timedelta
from decimal import Decimal
from typing import List

from sqlalchemy import func

from ...models_groupbuy import (COUPON_UNUSED, COUPON_USED, GroupbuyCoupon,
                                GroupbuyRedeemLog)
from . import verify as V
from .errors import CouponErrorCode, GroupbuyError
from .platform import (CouponQuery, PlatformAdapter, PlatformRedeemRecord,
                       RedeemResult, VerifyResult, register_adapter)

# 撤销时给原核销流水打的作废标记
REVERSED = "REVERSED"


def sign_payload(payload: dict, sign_key: str) -> str:
    """按自有平台的签名规则生成签名（回调路由与测试共用）。

    规则：
      1. 取 payload 中非空值（None/"" 除外）的键，按 key 的 ASCII 升序排列；
      2. 拼 ``k1=v1&k2=v2...``（值原样字符串化，不转义）；
      3. ``sign = hmac_sha256_hex(sign_key, 拼接串)`` 的小写十六进制。
    """
    if not sign_key:
        return ""
    items = sorted((k, str(v)) for k, v in (payload or {}).items()
                   if v not in (None, ""))
    text = "&".join("%s=%s" % (k, v) for k, v in items)
    return hmac.new(sign_key.encode("utf-8"), text.encode("utf-8"),
                    hashlib.sha256).hexdigest()


@register_adapter
class SelfPlatformAdapter(PlatformAdapter):
    """自有小程序平台。platform_code = ``self``。"""

    platform_code = "self"

    # ---------- 内部 ----------
    @staticmethod
    def _new_db():
        from ...db import SessionLocal
        return SessionLocal()

    def _find(self, db, req: CouponQuery):
        """按 (租户, 平台, 券码) 取券；不存在直接抛 COUPON_NOT_FOUND。"""
        code = V.check_coupon_code_format(req.coupon_code)
        row = (db.query(GroupbuyCoupon)
                 .filter(GroupbuyCoupon.tenant_id == req.tenant_id,
                         GroupbuyCoupon.coupon_code == code)
                 .first())
        if row is None:
            raise GroupbuyError(CouponErrorCode.COUPON_NOT_FOUND)
        return row

    # ---------- 验券 ----------
    def verify_coupon(self, req: CouponQuery) -> VerifyResult:
        db = self._new_db()
        try:
            row = self._find(db, req)
            return V.verify_state(row, req)
        except GroupbuyError as e:
            return VerifyResult(False, error_code=e.code, message=e.message)
        finally:
            db.close()

    # ---------- 核销 ----------
    def redeem_coupon(self, req: CouponQuery) -> RedeemResult:
        db = self._new_db()
        try:
            row = self._find(db, req)
            v = V.verify_state(row, req)
            if not v.valid:
                return RedeemResult(False, error_code=v.error_code, message=v.message)

            discount = V.check_actual_discount(
                req.actual_discount, Decimal(row.discount_amount or 0))
            now = datetime.now()

            # 条件更新抢占：只有 status 仍是「待核销」时才改得掉。
            # 并发下第二个请求受影响行数为 0 -> 已被别人核销。
            claimed = (db.query(GroupbuyCoupon)
                         .filter(GroupbuyCoupon.id == row.id,
                                 GroupbuyCoupon.status == COUPON_UNUSED)
                         .update({"status": COUPON_USED,
                                  "store_id": req.store_id,
                                  "order_id": req.order_id or 0,
                                  "actual_discount": discount,
                                  "redeemed_at": now,
                                  "redeemed_by": req.operator_id or 0},
                                 synchronize_session=False))
            if not claimed:
                db.rollback()
                return RedeemResult(False, error_code=str(CouponErrorCode.COUPON_USED),
                                    message="该券已被核销（并发冲突）")
            db.add(GroupbuyRedeemLog(
                tenant_id=req.tenant_id, store_id=req.store_id, coupon_id=row.id,
                coupon_code=row.coupon_code, platform_code=self.platform_code,
                order_id=req.order_id or 0, actual_discount=discount,
                action="redeem", operator_id=req.operator_id or 0))
            db.commit()

            db.refresh(row)
            return RedeemResult(True, coupon=V._to_info(row), redeem_id=row.id,
                                actual_discount=discount)
        except GroupbuyError as e:
            db.rollback()
            return RedeemResult(False, error_code=e.code, message=e.message)
        finally:
            db.close()

    # ---------- 撤销核销 ----------
    def reverse_redeem(self, req: CouponQuery) -> RedeemResult:
        """撤销后券回到「待核销」，可以重新核销。撤销人/原因记流水。"""
        if not (req.reason or "").strip():
            return RedeemResult(False, error_code="REVERSE_REASON_REQUIRED",
                                message="撤销核销必须填写原因")
        db = self._new_db()
        try:
            row = self._find(db, req)
            if row.status != COUPON_USED:
                return RedeemResult(False, error_code="ALREADY_REVERSED",
                                    message="该券未核销，无需撤销")

            # 同样用条件更新抢占，防止「撤销 + 核销」并发打架
            claimed = (db.query(GroupbuyCoupon)
                         .filter(GroupbuyCoupon.id == row.id,
                                 GroupbuyCoupon.status == COUPON_USED)
                         .update({"status": COUPON_UNUSED,
                                  "store_id": 0, "order_id": 0,
                                  "actual_discount": Decimal(0),
                                  "redeemed_at": None, "redeemed_by": 0,
                                  "reversed_at": datetime.now(),
                                  "reversed_by": req.operator_id or 0},
                                 synchronize_session=False))
            if not claimed:
                db.rollback()
                return RedeemResult(False, error_code=str(CouponErrorCode.COUPON_USED),
                                    message="该券状态已变化，撤销失败")

            # 原核销流水作废（对账不再计入），并记一条撤销流水
            (db.query(GroupbuyRedeemLog)
               .filter(GroupbuyRedeemLog.coupon_id == row.id,
                       GroupbuyRedeemLog.action == "redeem",
                       GroupbuyRedeemLog.reason != REVERSED)
               .update({"reason": REVERSED}, synchronize_session=False))
            db.add(GroupbuyRedeemLog(
                tenant_id=req.tenant_id, store_id=req.store_id, coupon_id=row.id,
                coupon_code=row.coupon_code, platform_code=self.platform_code,
                order_id=0, actual_discount=Decimal(0),
                action="reverse", reason=req.reason[:255],
                operator_id=req.operator_id or 0))
            db.commit()

            db.refresh(row)
            return RedeemResult(True, coupon=V._to_info(row), redeem_id=row.id)
        except GroupbuyError as e:
            db.rollback()
            return RedeemResult(False, error_code=e.code, message=e.message)
        finally:
            db.close()

    # ---------- 对账 ----------
    def reconcile(self, req: CouponQuery, day: datetime) -> List[PlatformRedeemRecord]:
        """自有平台「平台侧记录」= 本地未作废的核销流水。"""
        db = self._new_db()
        try:
            day_start = day.replace(hour=0, minute=0, second=0, microsecond=0)
            day_end = day_start + timedelta(days=1)
            rows = (db.query(GroupbuyRedeemLog)
                      .filter(GroupbuyRedeemLog.tenant_id == req.tenant_id,
                              GroupbuyRedeemLog.platform_code == self.platform_code,
                              GroupbuyRedeemLog.action == "redeem",
                              GroupbuyRedeemLog.reason != REVERSED,
                              GroupbuyRedeemLog.created_at >= day_start,
                              GroupbuyRedeemLog.created_at < day_end)
                      .all())
            return [PlatformRedeemRecord(
                platform_code=self.platform_code,
                coupon_code=r.coupon_code,
                redeem_time=r.created_at,
                discount_amount=Decimal(r.actual_discount or 0),
                order_no=str(r.order_id or ""),
            ) for r in rows]
        finally:
            db.close()

    # ---------- 签名 ----------
    def verify_sign(self, payload: dict, sign: str, sign_key: str) -> bool:
        """HMAC-SHA256 验签；常量时间比较，sign_key 用完即弃、绝不外泄。"""
        if not sign_key:
            return False
        expect = sign_payload(payload, sign_key)
        return bool(expect) and hmac.compare_digest(expect, (sign or "").strip().lower())
