"""
legacy —— 参考系统客户端兼容层（Bridge）

作用：让**兼容旧协议的客户端**不用改一行代码就能连到本服务端，
用于 M1 阶段快速验证「登录 → 商品 → 点单 → 结账」闭环。
新客户端请直接使用 /api/v1/* 契约。
"""
import json
import os
import uuid
from datetime import datetime

from fastapi import APIRouter, Depends, Request
from sqlalchemy.orm import Session

from ..config import OSS_DIR
from ..db import get_db
from ..models import Store, SysUser, Tenant, Terminal
from ..util import md5_16, ok

router = APIRouter(tags=["legacy"])


def _envelope(data, code="200", msg="ok", success=True):
    """同时兼容 ResponseBase<T>(code/msg/data) 与 ApiResponse(data/success/message)"""
    return {"code": code, "msg": msg, "message": msg, "success": success,
            "key_sign": "", "trace_no": uuid.uuid4().hex, "data": data}


def _envelope_str(data_obj, code=200, msg="ok"):
    """
    真实服务端格式：data 是**二次编码的 JSON 字符串**
    例：{"code":200,"data":"{\\"areaList\\":[...]}","key_sign":"...","success":true,"trace_no":"..."}
    """
    return {"code": code, "msg": msg, "message": msg, "success": True,
            "key_sign": "", "trace_no": uuid.uuid4().hex,
            "data": json.dumps(data_obj, ensure_ascii=False) if data_obj is not None else None}


def _pick_store(db: Session):
    tenant = db.query(Tenant).filter(Tenant.status == 1).order_by(Tenant.id).first()
    store = db.query(Store).filter(Store.status == 1).order_by(Store.id).first()
    user = db.query(SysUser).filter(SysUser.status == 1).order_by(SysUser.id).first()
    return tenant, store, user


def _login_payload(db: Session) -> dict:
    tenant, store, user = _pick_store(db)
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
    await request.body()
    payload = _login_payload(db)
    if payload is None:
        return _envelope(None, code="500", msg="未初始化租户/门店，请先执行 seed", success=False)
    return _envelope(payload)


@router.post("/saas/loginConfirm")
async def legacy_login_confirm(request: Request, db: Session = Depends(get_db)):
    await request.body()
    payload = _login_payload(db)
    return _envelope(payload)


@router.get("/saas/refreshJwt")
def legacy_refresh_jwt():
    return _envelope(uuid.uuid4().hex)


@router.post("/saas/logout")
def legacy_logout():
    return _envelope("ok")


@router.post("/pos/api/incr/productData")
async def legacy_incr_product(request: Request):
    body = await request.json() if await request.body() else {}
    return _envelope({"incrDataResult": [
        {"resultCode": "02", "downloadDataType": body.get("downloadDataType", "")}]})


@router.post("/store/getStoreConfig/")
def legacy_store_config(db: Session = Depends(get_db)):
    _t, store, _u = _pick_store(db)
    return _envelope({"storeId": str(store.id) if store else "",
                      "storeName": store.name if store else "",
                      "storeCode": store.code if store else "",
                      "storeMode": 1, "storeType": 1, "tradeType": 2, "verifyStatus": 1})


@router.get("/store/getStoreConfig/")
def legacy_store_config_get(db: Session = Depends(get_db)):
    return legacy_store_config(db)


@router.post("/pos/operate/getOperatelist")
def legacy_operate_list():
    return _envelope([])


# ====================================================================
#  以下按「真实生产环境抓包」的契约实现（见 （略）
#  要点：data 是二次编码的 JSON 字符串
# ====================================================================

@router.post("/pos/area/list")
def legacy_area_list(db: Session = Depends(get_db)):
    """真实响应：{"data":"{\\"areaList\\":[{\\"areaName\\":\\"大厅\\",\\"id\\":\\"32982\\",\\"sort\\":0,\\"deskCount\\":1}]}"}"""
    _t, store, _u = _pick_store(db)
    areas = []
    if store:
        from ..models import Desk, DeskArea
        rows = (db.query(DeskArea).filter(DeskArea.store_id == store.id)
                  .order_by(DeskArea.sort.asc(), DeskArea.id.asc()).all())
        for a in rows:
            cnt = db.query(Desk).filter(Desk.store_id == store.id,
                                        Desk.area_id == a.id).count()
            areas.append({"areaName": a.name, "id": str(a.id),
                          "sort": a.sort or 0, "deskCount": cnt})
    return _envelope_str({"areaList": areas})


@router.post("/pos/desk/list")
def legacy_desk_list(db: Session = Depends(get_db)):
    """
    真实响应：data 为 JSON 字符串，元素字段（截取自生产抓包）：
      seatsNum currDeskName memo deskTag deskName areaName deskTime childrenNum id
      userReceiveAmt orderNo customerNum dataStatus updateTime paidAmt sort storeId
      isReverseCheck version orderDeskNo deskStatus memberNo areaId createTime
      tenantId memberCardNo deskId
    """
    _t, store, _u = _pick_store(db)
    out = []
    if store:
        from ..models import Desk, DeskArea
        now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        areas = {a.id: a.name for a in
                 db.query(DeskArea).filter(DeskArea.store_id == store.id).all()}
        for d in (db.query(Desk).filter(Desk.store_id == store.id)
                    .order_by(Desk.area_id.asc(), Desk.id.asc()).all()):
            out.append({
                "id": str(d.id), "deskId": str(d.id), "deskName": d.name,
                "currDeskName": d.name, "deskTag": "", "memo": "",
                "areaId": str(d.area_id or 0), "areaName": areas.get(d.area_id, ""),
                "seatsNum": d.capacity or 4, "childrenNum": 0, "customerNum": 0,
                "deskStatus": 1 if d.status == 1 else 0,
                "dataStatus": 1, "sort": 0, "version": 1,
                "orderNo": "", "orderDeskNo": "",
                "userReceiveAmt": "0.0000", "paidAmt": "0.0000",
                "memberNo": "", "memberCardNo": "", "isReverseCheck": 0,
                "storeId": str(store.id), "tenantId": str(store.tenant_id),
                "createTime": now, "updateTime": now, "deskTime": now,
            })
    return _envelope_str(out)


@router.post("/pos/store/info")
def legacy_store_info(db: Session = Depends(get_db)):
    _t, store, _u = _pick_store(db)
    return _envelope_str({
        "dinnerAcceptWay": 1, "autoClearTable": 0, "scanSwitch": 1,
        "storeId": str(store.id) if store else "",
        "storeName": store.name if store else "",
        "storeCode": store.code if store else "",
        "storeMode": store.store_mode if store else 1,
    })


@router.post("/pos/surcharge/query")
def legacy_surcharge_query():
    """真实响应 data 为 "[]" """
    return _envelope_str([])


@router.get("/legacy/_paths")
def legacy_paths(db: Session = Depends(get_db)):
    tenant, store, _u = _pick_store(db)
    if not tenant or not store:
        return ok(None)
    return ok({
        "tenantMd5": md5_16(tenant.id), "storeMd5": md5_16(store.id),
        "ossDir": os.path.join(OSS_DIR, md5_16(tenant.id), md5_16(store.id), "cyproduct"),
    })


# ---- 兜底：未实现的参考系统接口一律返回成功空数据，避免客户端登录链中断 ----
@router.post("/{full_path:path}")
async def legacy_catch_all(full_path: str, request: Request):
    if full_path.startswith("api/v1/"):
        return _envelope(None, code="404", msg="no such api", success=False)
    try:
        await request.body()
    except Exception:
        pass
    return _envelope({})
