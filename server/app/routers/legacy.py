"""
legacy —— 参考系统客户端兼容层（Bridge）

作用：让**兼容旧协议的客户端**不用改一行代码就能连到本服务端，
用于 M1 阶段快速验证「登录 → 商品 → 点单 → 结账」闭环。
新客户端请直接使用 /api/v1/* 契约。

覆盖任务卡 P0-1 ~ P0-5 中的基础接口：
    /saas/login、/saas/loginConfirm、/saas/refreshJwt、/saas/logout
    /store/getStoreConfig/
    /pos/api/incr/productData
    /pos/operate/getOperatelist
    /pos/area/list、/pos/desk/list、/pos/store/info、/pos/surcharge/query

桌台 / 订单 / 交班在 legacy_order.py，打印与功能开关在 legacy_print.py。
本模块的 `/{full_path:path}` 兜底路由必须**最后注册**（见 app/main.py）。
"""
import os
import uuid

from fastapi import APIRouter, Depends, Request
from sqlalchemy.orm import Session

from ..config import OSS_DIR
from ..db import get_db
from ..legacy_core import (desk_dto, envelope, kv_get_or_init, parse_payload,
                           pick_store, resolve_scope, response_base, s)
from ..models import (Desk, DeskArea, Product, ProductCategory, ProductUnit,
                      Store, Terminal)
from ..util import md5_16, ok

router = APIRouter(tags=["legacy"])

# 兼容旧调用点
_envelope = envelope
_pick_store = pick_store


def _envelope_str(data_obj, code=200, msg="ok"):
    """格式二：data 是**二次编码的 JSON 字符串**（envelope 已按此实现）。"""
    return envelope(data_obj, code=code, msg=msg)


# --------------------------------------------------------------- 登录

def _login_payload(db: Session) -> dict:
    tenant, store, user = pick_store(db)
    if not tenant or not store:
        return None
    term = db.query(Terminal).filter(Terminal.store_id == store.id).order_by(Terminal.id).first()
    terminal_no = term.no if term else "T0001"
    token = uuid.uuid4().hex * 2          # 64 字符，满足客户端 keyVector>=32 的约束
    return {
        "agentId": "", "brandId": "", "brandNo": "",
        "merchantNo": store.code, "merchantName": store.name,
        "terminalNo": terminal_no, "terminalToken": "",
        "productkey": "", "deviceName": "POS-" + terminal_no, "deviceSecret": "",
        "accountName": user.account if user else "admin",
        "roleName": "店长", "cashierRoleName": "收银员",
        "roleType": "1", "userType": "1",
        "userId": str(user.id if user else 1),
        "loginToken": token, "keySign": "",
        "accessStatus": 1, "activationStatus": 1,
        "deviceExpireDate": "2126-10-12 00:00:00",
        "refreshToken": token,
        "user_info": {
            "verifyStatus": 1, "version": "1",
            # tradeType: 0/1 = 零售（客户端会跳到「无法登录本产品」提示页），>=2 才是餐饮
            "tradeType": 2, "storeType": 1, "storeMode": store.store_mode or 1,
            "storeId": str(store.id), "storeCode": store.code, "storeName": store.name,
            "tenantId": str(tenant.id), "tenantCode": tenant.code, "tenantName": tenant.name,
            "posNo": terminal_no, "posToken": "",
            "expireDate": "2126-10-12 00:00:00", "systemVersion": 1,
        },
        "fcUrl": "", "fcToken": "",
        "userList": [{
            "accountName": user.account if user else "admin", "roleName": "店长",
            "userType": 1, "userId": str(user.id if user else 1),
            "merchantNo": store.code, "storeName": store.name,
        }],
        "systemData": None,
        "storeId": str(store.id), "storeCode": store.code, "storeName": store.name,
        "memberOpenSwitch": 1, "appConfigId": "1", "oemCode": "", "fcMsgMaxNo": "0",
    }


@router.post("/saas/login")
async def legacy_login(request: Request, db: Session = Depends(get_db)):
    """真实抓包：{"code":200,"msg":"成功.","data":{...}} —— 格式一，data 是对象。"""
    await request.body()
    payload = _login_payload(db)
    if payload is None:
        return response_base(None, code=500, msg="未初始化租户/门店，请先执行 seed")
    return response_base(payload, code=200, msg="成功.")


@router.post("/saas/loginConfirm")
async def legacy_login_confirm(request: Request, db: Session = Depends(get_db)):
    await request.body()
    return response_base(_login_payload(db), code=200, msg="成功.")


@router.get("/saas/refreshJwt")
def legacy_refresh_jwt():
    """真实抓包：{"code":200,"msg":"成功.","data":"<JWT>"}"""
    return response_base(uuid.uuid4().hex * 2, code=200, msg="成功.")


@router.post("/saas/logout")
def legacy_logout():
    return response_base("ok", code=200, msg="成功.")


# ------------------------------------------------------- 门店配置（格式一）

@router.post("/store/getStoreConfig/")
@router.get("/store/getStoreConfig/")
def legacy_store_config(request: Request, db: Session = Depends(get_db)):
    """真实响应：{"code":200,"msg":"成功","data":{"openCashboxControl":0,
    "refundVerifyStatus":0,"openMemberPayStat":0}} —— 格式一，data 是对象。"""
    scope = resolve_scope(db, {}, {}, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id, "store.config",
                          {"openCashboxControl": 0, "refundVerifyStatus": 0,
                           "openMemberPayStat": 0})
    return {"code": 200, "msg": "成功", "message": "成功", "data": data}


# ------------------------------------------------------- 商品增量同步

# downloadDataType → 版本来源表
_INCR_MODELS = {
    "CyProduct": Product,
    "CyProductUnit": ProductUnit,
    "CyProductCategory": ProductCategory,
}

_INCR_BASE_VERSION = 1791772770000


def _latest_version(db: Session, tenant_id: int, data_type: str) -> str:
    model = _INCR_MODELS.get(data_type)
    if model is not None:
        row = (db.query(model.version)
                 .filter(model.tenant_id == tenant_id)
                 .order_by(model.version.desc()).first())
        if row and row[0]:
            return str(int(row[0]))
    # 会员 / 桌台 / 支付方式等没有独立版本表，统一用一个稳定版本号
    return str(_INCR_BASE_VERSION)


@router.post("/pos/api/incr/productData")
async def legacy_incr_product(request: Request, db: Session = Depends(get_db)):
    """真实响应：{"incrDataResult":[{"latestVersion":"1791772770000",
    "downloadDataType":"CyProductUnit","currVersion":"0","resultCode":"03",
    "incrFileName":"CyProductUnit_0_1791772770000.csv"}]}

    resultCode：01 = 已是最新，无需下载；03 = 有增量，按 incrFileName 下载。
    """
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    curr = s(payload.get("currVersion"), "0")
    dtype = s(payload.get("downloadDataType"))
    latest = _latest_version(db, scope.tenant_id, dtype) if dtype else "0"
    if not dtype:
        return envelope({"incrDataResult": []})
    need = curr != latest
    item = {"latestVersion": latest, "downloadDataType": dtype,
            "currVersion": curr, "resultCode": "03" if need else "01",
            "incrFileName": "%s_%s_%s.csv" % (dtype, curr, latest) if need else ""}
    return envelope({"incrDataResult": [item]})


# ------------------------------------------------------- 操作原因字典

# type：1 通用 / 2、3 改价免单 / 4、6、7 加急 / 5 退菜 / 9 订单来源
#       10 外送 / 11 客户类型 / 12 订单异常 / 13 退款换菜
DEFAULT_OPERATE_REASONS = {
    1: ["操作失误", "顾客要求", "其他"],
    2: ["促销活动", "顾客生日", "顾客投诉", "出餐时间长"],
    3: ["促销活动", "顾客生日", "顾客投诉", "出餐时间长", "友情赠送"],
    4: ["加急制作", "延迟出餐"],
    5: ["有异物", "变质或有异味", "上错菜", "顾客点错菜", "用餐人数变化", "出餐时间长"],
    6: ["加急制作", "延迟出餐"],
    7: ["加急制作", "延迟出餐"],
    8: ["其他"],
    9: ["促销活动", "顾客生日", "顾客投诉", "出餐时间长"],
    10: ["已和顾客电话沟通", "餐已开始制作", "餐已做正在配送中", "其他"],
    11: ["老板朋友", "顾客生日", "领导", "重要顾客"],
    12: ["操作失误", "菜品问题", "顾客要求", "无法接单"],
    13: ["退菜或退款", "更换菜品", "更换优惠活动", "更换结账方式"],
}


def _default_operates(scope) -> list:
    out = []
    for otype in sorted(DEFAULT_OPERATE_REASONS):
        for idx, desc in enumerate(DEFAULT_OPERATE_REASONS[otype]):
            out.append({
                "description": desc,
                "id": int(scope.store_id) * 1000 + otype * 10 + idx,
                "sort": idx,
                "type": otype,
            })
    return out


@router.post("/pos/operate/getOperatelist")
async def legacy_operate_list(request: Request, db: Session = Depends(get_db)):
    """真实响应 data 为 [{description,id,sort,type}]，按门店隔离。"""
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id, "pos.operate",
                          _default_operates(scope))
    return envelope(data)


# ------------------------------------------------------- 桌台 / 门店

@router.post("/pos/area/list")
async def legacy_area_list(request: Request, db: Session = Depends(get_db)):
    """真实响应：{"data":"{\\"areaList\\":[{\\"areaName\\":\\"大厅\\",\\"id\\":\\"32982\\",\\"sort\\":0,\\"deskCount\\":1}]}"}"""
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    rows = (db.query(DeskArea).filter(DeskArea.store_id == scope.store_id)
              .order_by(DeskArea.sort.asc(), DeskArea.id.asc()).all())
    areas = []
    for a in rows:
        cnt = (db.query(Desk)
                 .filter(Desk.store_id == scope.store_id, Desk.area_id == a.id)
                 .count())
        areas.append({"areaName": a.name, "id": str(a.id),
                      "sort": a.sort or 0, "deskCount": cnt})
    return envelope({"areaList": areas})


@router.post("/pos/desk/list")
async def legacy_desk_list(request: Request, db: Session = Depends(get_db)):
    """真实响应 data 为 JSON 字符串，元素见 legacy_core.desk_dto。"""
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    rows = (db.query(Desk).filter(Desk.store_id == scope.store_id)
              .order_by(Desk.area_id.asc(), Desk.id.asc()).all())
    return envelope([desk_dto(db, d, scope) for d in rows])


@router.post("/pos/store/info")
async def legacy_store_info(request: Request, db: Session = Depends(get_db)):
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    store = db.get(Store, scope.store_id)
    return envelope({
        "dinnerAcceptWay": 1, "autoClearTable": 0, "scanSwitch": 1,
        "storeId": str(scope.store_id),
        "storeName": store.name if store else "",
        "storeCode": store.code if store else "",
        "storeMode": (store.store_mode if store else 1) or 1,
    })


@router.post("/pos/surcharge/query")
async def legacy_surcharge_query(request: Request, db: Session = Depends(get_db)):
    """真实响应 data 为 "[]"；门店自定义附加费存在 KV 里。"""
    payload, meta = parse_payload(await _safe_json(request))
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id, "pos.surcharge", [])
    return envelope(data)


@router.get("/legacy/_paths")
def legacy_paths(db: Session = Depends(get_db)):
    tenant, store, _u = pick_store(db)
    if not tenant or not store:
        return ok(None)
    return ok({
        "tenantMd5": md5_16(tenant.id), "storeMd5": md5_16(store.id),
        "ossDir": os.path.join(OSS_DIR, md5_16(tenant.id), md5_16(store.id), "cyproduct"),
    })


async def _safe_json(request: Request) -> dict:
    """旧客户端 POST 有时不带 body / 带 form，统一兜底成 dict。"""
    try:
        body = await request.json()
    except Exception:
        body = None
    return body if isinstance(body, dict) else {}


# ---- 兜底：未实现的参考系统接口一律返回成功空数据，避免客户端登录链中断 ----
# ⚠️ 必须最后注册（app/main.py 里 legacy.router 放在最后 include）
@router.post("/{full_path:path}")
async def legacy_catch_all(full_path: str, request: Request):
    if full_path.startswith("api/v1/"):
        return envelope(None, code=404, msg="no such api", success=False)
    await request.body()
    return envelope({})
