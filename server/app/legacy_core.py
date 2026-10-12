"""
legacy_core —— 参考系统客户端兼容层公共件

真实客户端（已打补丁的 Windows 收银端）的报文形态，取自
生产环境抓包：

    请求  {"api_ver":"100","sign_type":"4","terminal_no":"","trace_no":"...",
           "user_id":"16199115","pos_no":"30060376",
           "data":"{\\"deskId\\":\\"50838\\"}",     ← 业务字段二次编码进 data
           "key_sign":"..."}

    响应  {"code":200,"data":"{\\"orderDesk\\":{...}}","key_sign":"",
           "success":true,"trace_no":"..."}       ← data 同样是二次编码的 JSON 字符串

登录类接口用 ResponseBase：`{"code":"200","msg":"成功","data":{...}}`（data 是对象）。

本模块只做「解析 / 封装 / 取租户上下文 / 通用 KV」四件事，
不含任何业务逻辑，供 legacy.py / legacy_order.py / legacy_print.py 共用。
"""
import json
import uuid
from datetime import datetime
from typing import Any, Optional, Tuple

from sqlalchemy.orm import Session

from .models import (Desk, DeskArea, Dictionary, Order, OrderItem, Payment,
                     Store, SysUser, Terminal, Tenant)

# --------------------------------------------------------------------
#  枚举映射（对齐兼容客户端的 OrderStatus / PaymentType）
# --------------------------------------------------------------------

# 客户端 OrderStatus：0全部 1未结账 2结账 3已退款 4已撤单
# 服务端 status     ：0待结   1已结   2已退   3作废
STATUS_TO_LEGACY = {0: 1, 1: 2, 2: 3, 3: 4}
STATUS_FROM_LEGACY = {1: 0, 2: 1, 3: 2, 4: 3}

# 客户端 PaymentType（pmSign 就是它的数字字符串）
#   0其他 1扫码 2现金 3会员 4离线码 5组合 8代金券 9积分 999N7
PAY_NAME = {"0": "OTHER", "1": "SCAN", "2": "CASH", "3": "MEMBER", "4": "QRCODE"}
PAY_SIGN = {v: k for k, v in PAY_NAME.items()}
PAY_NAME_CN = {"OTHER": "其他", "SCAN": "扫码支付", "CASH": "现金支付",
               "MEMBER": "会员支付", "QRCODE": "离线码支付"}

# 业务上认得的别名（新契约客户端直接发 CASH / MEMBER 这种名字）
_PAY_ALIAS = {
    "CASH": "CASH", "现金": "CASH", "现金支付": "CASH", "2": "CASH",
    "MEMBER": "MEMBER", "会员": "MEMBER", "会员支付": "MEMBER", "3": "MEMBER",
    "SCAN": "SCAN", "扫码": "SCAN", "扫码支付": "SCAN", "1": "SCAN",
    "QRCODE": "QRCODE", "离线码": "QRCODE", "离线码支付": "QRCODE", "4": "QRCODE",
    "OTHER": "OTHER", "其他": "OTHER", "0": "OTHER", "000": "OTHER", "": "OTHER",
}


def pay_name(v: Any) -> str:
    """把任意形态的支付方式归一成 CASH / MEMBER / SCAN / QRCODE / OTHER。"""
    s = str(v or "").strip()
    return _PAY_ALIAS.get(s.upper(), _PAY_ALIAS.get(s, "OTHER"))


def pay_sign(v: Any) -> str:
    """支付方式 → 客户端 pmSign 字符串（"2" 现金 / "3" 会员 …）。"""
    return PAY_SIGN.get(pay_name(v), "0")


def now_str() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def ts_str(v) -> str:
    return v.strftime("%Y-%m-%d %H:%M:%S") if v else ""


# --------------------------------------------------------------------
#  类型强转：客户端把 id / 金额统统塞成字符串，必须容忍
# --------------------------------------------------------------------

def i(v, default: int = 0) -> int:
    try:
        if v is None or v == "":
            return default
        return int(float(v))
    except (TypeError, ValueError):
        return default


def f(v, default: float = 0.0) -> float:
    try:
        if v is None or v == "":
            return default
        return round(float(v), 2)
    except (TypeError, ValueError):
        return default


def s(v, default: str = "") -> str:
    return default if v is None else str(v)


def q(v, default: float = 1.0) -> float:
    """数量：保留 3 位小数（和 QTY 列一致）。"""
    try:
        if v is None or v == "":
            return default
        return round(float(v), 3)
    except (TypeError, ValueError):
        return default


def money(v) -> str:
    """客户端大量字段是 4 位小数的字符串金额。"""
    return "%.4f" % f(v)


# --------------------------------------------------------------------
#  报文解析
# --------------------------------------------------------------------

_META_KEYS = ("api_ver", "apiVer", "sign_type", "signType", "terminal_no", "terminalNo",
              "trace_no", "traceNo", "user_id", "userId", "pos_no", "posNo",
              "key_sign", "keySign", "timestamp", "deviceMac", "deviceType")


def parse_payload(body: Any) -> Tuple[dict, dict]:
    """拆成 (业务载荷, 元信息)。

    客户端把业务字段二次编码进 `data`；冒烟测试 / 新客户端直接平铺。
    两种都支持：data 里的字段先铺开，平铺字段再覆盖（便于联调时打补丁）。
    """
    body = body if isinstance(body, dict) else {}
    meta = {k: body[k] for k in _META_KEYS if body.get(k) is not None}
    payload: dict = {}

    raw = body.get("data")
    if isinstance(raw, str) and raw.strip():
        try:
            decoded = json.loads(raw)
            if isinstance(decoded, dict):
                payload = decoded
        except (ValueError, TypeError):
            payload = {}
    elif isinstance(raw, dict):
        payload = dict(raw)

    for k, v in body.items():
        if k not in _META_KEYS and k != "data":
            payload[k] = v
    return payload, meta


# --------------------------------------------------------------------
#  响应封装
# --------------------------------------------------------------------

def envelope(data, code=200, msg="ok", success=True, trace_no=None):
    """格式二 ApiResponse：`data` 是**二次编码的 JSON 字符串**。

    真实抓包：{"code":200,"data":"{\\"areaList\\":[...]}","key_sign":"","success":true,"trace_no":"..."}
    """
    return {
        "code": code,
        "msg": msg,
        "message": msg,
        "success": success,
        "key_sign": "",
        "trace_no": trace_no or uuid.uuid4().hex,
        "data": None if data is None else json.dumps(data, ensure_ascii=False, default=str),
    }


def response_base(data, code="200", msg="成功"):
    """格式一 ResponseBase：`data` 是对象本身（登录/门店配置类接口用）。"""
    return {"code": code, "msg": msg, "message": msg, "data": data}


def err(code: int, msg: str, data=None, trace_no=None):
    """业务失败：HTTP 仍为 200，靠 body 里的 code 表达（客户端只看 code）。"""
    return envelope(data, code=code, msg=msg, success=False, trace_no=trace_no)


# --------------------------------------------------------------------
#  租户 / 门店上下文（多租户隔离的唯一入口）
# --------------------------------------------------------------------

class Scope:
    """一次请求的归属：所有查询都必须带上 tenant_id / store_id。"""

    __slots__ = ("tenant_id", "store_id", "terminal_no", "user_id")

    def __init__(self, tenant_id: int, store_id: int, terminal_no: str, user_id: int):
        self.tenant_id = tenant_id
        self.store_id = store_id
        self.terminal_no = terminal_no
        self.user_id = user_id

    def __repr__(self):  # pragma: no cover - 调试用
        return "Scope(tenant=%s, store=%s, term=%s, user=%s)" % (
            self.tenant_id, self.store_id, self.terminal_no, self.user_id)


def resolve_scope(db: Session, payload: dict = None, meta: dict = None,
                  request=None) -> Scope:
    """定位本次请求属于哪个租户 / 门店 / 终端。

    优先级：
      1. 显式头 X-Store-Id / X-Tenant-Id（联调与隔离测试用）
      2. 报文里的 storeId / tenantId
      3. pos_no / terminal_no 反查 terminal 表
      4. 兜底：第一个启用中的门店
    """
    payload = payload or {}
    meta = meta or {}
    hdr = request.headers if request is not None else {}

    store_id = (i(hdr.get("X-Store-Id")) or i(payload.get("storeId"))
                or i(meta.get("storeId")) or i(payload.get("store_id")))
    tenant_id = (i(hdr.get("X-Tenant-Id")) or i(payload.get("tenantId"))
                 or i(meta.get("tenantId")) or i(payload.get("tenant_id")))

    term_no = s(meta.get("terminalNo") or meta.get("terminal_no")
                or payload.get("posNo") or payload.get("terminalNo")
                or hdr.get("X-Terminal-No"))

    store = db.get(Store, store_id) if store_id else None
    if store is None and term_no:
        term = (db.query(Terminal).filter(Terminal.no == term_no)
                  .order_by(Terminal.id).first())
        if term is not None:
            store = db.get(Store, term.store_id)
    if store is None and tenant_id:
        store = (db.query(Store).filter(Store.tenant_id == tenant_id, Store.status == 1)
                   .order_by(Store.id).first())
    if store is None:
        store = db.query(Store).filter(Store.status == 1).order_by(Store.id).first()

    if store is not None:
        store_id = int(store.id)
        tenant_id = int(store.tenant_id)
        if not term_no:
            term = (db.query(Terminal).filter(Terminal.store_id == store_id)
                      .order_by(Terminal.id).first())
            term_no = term.no if term else "T0001"
    else:                                   # 空库：交给调用方返回 500
        store_id, tenant_id, term_no = 0, 0, term_no or "T0001"

    user_id = i(meta.get("userId") or meta.get("user_id"))
    if not user_id and tenant_id:
        u = (db.query(SysUser).filter(SysUser.tenant_id == tenant_id, SysUser.status == 1)
               .order_by(SysUser.id).first())
        user_id = int(u.id) if u else 0
    return Scope(tenant_id, store_id, term_no, user_id)


def pick_store(db: Session):
    """老接口用的兜底三元组（保持向后兼容）。"""
    tenant = db.query(Tenant).filter(Tenant.status == 1).order_by(Tenant.id).first()
    store = db.query(Store).filter(Store.status == 1).order_by(Store.id).first()
    user = db.query(SysUser).filter(SysUser.status == 1).order_by(SysUser.id).first()
    return tenant, store, user


# --------------------------------------------------------------------
#  通用 KV（挂在 dictionary_table 上，不新增表）
# --------------------------------------------------------------------

def kv_get(db: Session, tenant_id: int, store_id: int, key: str, default=None):
    row = (db.query(Dictionary)
             .filter(Dictionary.tenant_id == tenant_id, Dictionary.store_id == store_id,
                     Dictionary.key == key).first())
    if row is None or row.value is None:
        return default
    try:
        return json.loads(row.value)
    except (ValueError, TypeError):
        return default


def kv_set(db: Session, tenant_id: int, store_id: int, key: str, value) -> None:
    text = json.dumps(value, ensure_ascii=False, default=str)
    row = (db.query(Dictionary)
             .filter(Dictionary.tenant_id == tenant_id, Dictionary.store_id == store_id,
                     Dictionary.key == key).first())
    if row is None:
        db.add(Dictionary(tenant_id=tenant_id, store_id=store_id, key=key, value=text))
    else:
        row.value = text


def kv_get_or_init(db: Session, tenant_id: int, store_id: int, key: str, default):
    """读配置；没有就落一份默认值（打印模板/策略这类静态配置用）。"""
    value = kv_get(db, tenant_id, store_id, key)
    if value is None:
        kv_set(db, tenant_id, store_id, key, default)
        db.commit()
        return default
    return value


ORDER_EXTRA_PREFIX = "order_extra:"


def save_order_extra(db: Session, o: Order, extra: dict) -> None:
    """把模型里没有对应列的订单子对象（折扣/附加费/券/批次）存进通用 KV，
    保证 `/pos/order/detail` 能把客户端提交的内容原样还回去。"""
    if not extra:
        return
    kv_set(db, int(o.tenant_id), int(o.store_id), ORDER_EXTRA_PREFIX + o.order_no, extra)


def load_order_extra(db: Session, o: Order) -> dict:
    return kv_get(db, int(o.tenant_id), int(o.store_id),
                  ORDER_EXTRA_PREFIX + o.order_no, {}) or {}


# --------------------------------------------------------------------
#  订单序列化：服务端模型 → 客户端 OrderDetail
# --------------------------------------------------------------------

def _product_dto(it: OrderItem, order_no: str) -> dict:
    qty = float(it.qty or 0)
    price = float(it.price or 0)
    amt = float(it.amount or 0)
    disc = float(it.discount_amount or 0)
    return {
        "orderNo": order_no,
        "productSaleNo": str(it.id),
        "productId": str(it.product_id or 0),
        "channelProductId": str(it.product_id or 0),
        "productName": it.product_name or "",
        "specName": it.spec_text or "",
        "specId": "", "channelSpecId": "",
        "categoryId": "", "categoryName": "",
        "unitId": "", "unitName": "",
        "barCode": "", "productType": 1, "productSaleType": 1,
        "quantity": qty,
        "prodUnitAmt": price, "prodPrice": price,
        "prodSalesAmt": amt,
        "prodSubtotalAmt": amt, "prodSubtotalDiscAmt": disc,
        "prodDiscAmt": disc,
        "prodPaidAmt": round(amt - disc, 2),
        "prodTotalPaidAmt": round(amt - disc, 2),
        "prodTotalSubtotalAmt": amt,
        "packageUnitAmt": 0, "packageSalesAmt": 0, "packageSubtotalAmt": 0,
        "packageSubtotalDiscAmt": 0, "packageDiscAmt": 0, "packagePaidAmt": 0,
        "packageStatus": 0, "invStatus": 0, "slaveNum": 0,
        "memo": it.remark or "",
        "discFlag": 2 if it.gift_flag else 0,
        "refundStatus": 1 if it.status == 1 else 0,
        "refundQuantity": qty if it.status == 1 else 0,
        "orderProdStatus": 1 if it.status == 0 else 2,
        "productBatch": 0, "verifyNo": "",
        "productTime": "", "confirmTime": "", "prodCplTime": "",
        "printStrategy": 0, "floatingPrice": 0,
    }


def _pay_flow_dto(p: Payment, order_no: str, member_no: str = "") -> dict:
    name = pay_name(p.pay_type)
    status = int(p.status or 0)
    return {
        "orderNo": order_no,
        "payFlowNo": str(p.id),
        "payType": name,
        "pmSign": PAY_SIGN.get(name, "0"),
        "pmSignName": PAY_NAME_CN.get(name, "其他"),
        "outTradeNo": p.out_trade_no or "",
        "paymentAmt": float(p.amount or 0),
        "paidAmt": float(p.amount or 0),
        "discAmt": 0, "changeAmt": 0,
        "refundAmt": float(p.amount or 0) if status == 3 else 0,
        "poundageAmt": 0,
        "payStatusCode": 1 if status == 1 else (9 if status == 3 else 0),
        "confirmStatusCode": 1 if status == 1 else 0,
        "payTime": ts_str(p.pay_time),
        "terminalTime": ts_str(p.pay_time),
        "memberNo": member_no,
        "orderChannel": 0, "orderSource": 1,
    }


def _desk_dto(db: Session, o: Order, desk: Optional[Desk], scope: Scope) -> dict:
    d = desk
    area_name = ""
    if d is not None and d.area_id:
        a = db.get(DeskArea, d.area_id)
        area_name = a.name if a else ""
    return {
        "id": str(d.id) if d else "0",
        "orderDeskNo": o.order_no,
        "deskId": str(o.desk_id or 0),
        "deskName": (d.name if d else "") or "",
        "currDeskName": (d.name if d else "") or "",
        "deskTag": "", "memo": o.remark or "",
        "areaId": str(d.area_id or 0) if d else "0",
        "areaName": area_name,
        "seatsNum": (d.capacity or 0) if d else 0,
        "customerNum": 0, "childrenNum": 0,
        "sort": 0, "version": 1,
        "orderNo": o.order_no,
        "memberNo": "", "memberCardNo": "",
        "userReceiveAmt": money(0), "paidAmt": money(0),
        "deskTime": ts_str(o.created_at),
        "deskStatus": 1 if (d and d.status == 1) else 0,
        "dataStatus": 1, "isReverseCheck": 0,
        "storeId": str(scope.store_id), "tenantId": str(scope.tenant_id),
        "createDate": ts_str(o.created_at), "updateTime": ts_str(o.created_at),
    }


def order_detail(db: Session, o: Order, scope: Scope = None,
                 member_no: str = "", member_balance: float = 0) -> dict:
    """服务端 t_order → 客户端 OrderDetail（字段名对齐兼容客户端的 OrderDetail）。

    模型里没有独立列的 discounts / surcharges / coupons / batches 从通用 KV 回读，
    保证客户端提交什么、detail 就能拿回什么。
    """
    if scope is None:
        scope = Scope(int(o.tenant_id), int(o.store_id), "", int(o.operator_id or 0))

    items = (db.query(OrderItem).filter(OrderItem.order_id == o.id)
               .order_by(OrderItem.id).all())
    pays = (db.query(Payment).filter(Payment.order_id == o.id)
              .order_by(Payment.id).all())
    desk = db.get(Desk, o.desk_id) if o.desk_id else None
    extra = load_order_extra(db, o)

    prod_amt = round(sum(float(x.amount or 0) for x in items), 2)
    prod_disc = round(sum(float(x.discount_amount or 0) for x in items), 2)
    prod_paid = round(prod_amt - prod_disc, 2)
    paid = round(sum(float(p.amount or 0) for p in pays if int(p.status or 0) == 1), 2)
    refunded = round(sum(float(p.amount or 0) for p in pays if int(p.status or 0) == 3), 2)

    # paymentType 取已成功流水中金额最大的一种（客户端用数字枚举 PaymentType）
    ok_pays = [p for p in pays if int(p.status or 0) == 1]
    payment_type = 0
    if ok_pays:
        top = max(ok_pays, key=lambda p: float(p.amount or 0))
        payment_type = i(pay_sign(top.pay_type), 0)

    return {
        "orderNo": o.order_no,
        "channelOrderNo": extra.get("channelOrderNo", ""),
        "orderStatus": STATUS_TO_LEGACY.get(int(o.status or 0), 1),
        "orderType": int(o.order_type or 1),
        "orderSource": int(o.source or 1),
        "businessType": 1,
        "businessDate": (o.created_at.strftime("%Y-%m-%d") if o.created_at else ""),
        "orderTime": ts_str(o.created_at),
        "confirmTime": "", "prodCplTime": "", "pickUpTime": "", "traceTime": "",
        "payTime": ts_str(o.settle_time),
        "payStatus": 1 if int(o.status or 0) == 1 else 0,
        "paymentType": payment_type,
        "deskId": str(o.desk_id or 0),
        "deskName": (desk.name if desk else "") or "",
        "deskAreaName": (_area_name(db, desk) if desk else ""),
        "numberOfDiners": i(extra.get("customerNum")),
        "memberNo": member_no,
        "memberCardNo": member_no,
        "memberBalance": round(float(member_balance or 0), 2),
        "memberPoints": 0, "usePoints": 0,
        "amt": round(float(o.amount_total or 0), 2),
        "prodAmt": prod_amt,
        "prodPaidAmt": prod_paid,
        "discAmt": round(float(o.amount_discount or 0), 2),
        "discStoreAmt": 0,
        "paidAmt": paid,
        "receiveAmt": paid,
        "userReceiveAmt": paid,
        "serviceAmt": 0, "packageAmt": 0, "packagePaidAmt": 0, "packageType": 0,
        "deliveryAmt": 0, "deliveryPaidAmt": 0,
        "foodType": 0,
        "memo": o.remark or "",
        "cashierId": str(o.operator_id or 0),
        "cashierName": "",
        "operatorId": str(o.operator_id or 0),
        "operatorName": "",
        "posNo": scope.terminal_no,
        "optPosNo": scope.terminal_no,
        "shiftNo": str(o.shift_id or 0),
        "optShiftNo": str(o.shift_id or 0),
        "storeId": str(scope.store_id),
        "traceNo": "",
        "takeNo": "", "orderPushType": 0, "printTypeList": [],
        "autoRejectBatches": False,
        "api_ver": "100",
        "refundReason": "", "reverseSettleStatus": 0, "reverseSettleReason": "",
        "lcpayMerchantNo": "", "lcpayStoreId": "", "lcpayTerminalNo": "",
        "consumerName": "", "consumerPhone": "",
        "products": [_product_dto(x, o.order_no) for x in items],
        "payFlows": [_pay_flow_dto(p, o.order_no, member_no) for p in pays],
        "orderDesks": [_desk_dto(db, o, desk, scope)] if o.desk_id else [],
        "discounts": extra.get("discounts", []),
        "surcharges": extra.get("surcharges", []),
        "coupons": extra.get("coupons", []),
        "batches": extra.get("batches", []),
    }


def _area_name(db: Session, desk: Desk) -> str:
    if not desk or not desk.area_id:
        return ""
    a = db.get(DeskArea, desk.area_id)
    return a.name if a else ""


# 客户端 OrderDesk.deskStatus：1空闲 2占用 3预订 4/5其他占用 6已清台(dataStatus=99)
# 服务端 Desk.status      ：0空闲 1占用 2预订
DESK_STATUS_TO_LEGACY = {0: 1, 1: 2, 2: 3}
DESK_STATUS_FROM_LEGACY = {1: 0, 2: 1, 3: 2}


def desk_dto(db: Session, d: Desk, scope: Scope, o: Optional[Order] = None) -> dict:
    """服务端 Desk(+当前订单) → 客户端 OrderDesk（字段取自生产抓包）。"""
    area = db.get(DeskArea, d.area_id) if d.area_id else None
    if o is None and d.current_order_id:
        o = db.get(Order, d.current_order_id)
    extra = load_order_extra(db, o) if o is not None else {}
    return {
        "id": str(d.id), "deskId": str(d.id), "deskName": d.name or "",
        "currDeskName": d.name or "", "deskTag": "", "memo": (o.remark if o else "") or "",
        "areaId": str(d.area_id or 0), "areaName": (area.name if area else "") or "",
        "seatsNum": d.capacity or 4, "childrenNum": 0,
        "customerNum": i(extra.get("customerNum")) if o is not None else 0,
        "deskStatus": DESK_STATUS_TO_LEGACY.get(int(d.status or 0), 1),
        "dataStatus": 1, "sort": 0, "version": 1,
        "orderNo": (o.order_no if o else "") or "",
        "orderDeskNo": (extra.get("orderDeskNo") or (o.order_no if o else "")) or "",
        "orderNoMod": 0, "isReverseCheck": 0,
        "userReceiveAmt": money(o.amount_pay if o else 0),
        "paidAmt": money(o.amount_pay if o else 0),
        "memberNo": "", "memberCardNo": "",
        "storeId": str(scope.store_id), "tenantId": str(scope.tenant_id),
        "createTime": ts_str(o.created_at if o else None),
        "updateTime": ts_str(o.created_at if o else None),
        "deskTime": ts_str(o.created_at if o else None),
    }


# --------------------------------------------------------------------
#  幂等
# --------------------------------------------------------------------

def find_idempotent(db: Session, scope: Scope, client_order_no: str) -> Optional[Order]:
    """按 clientOrderNo 找原单据；命中即说明是重传，调用方应回 409 + 原单据。

    过滤条件同时带 store_id，避免跨门店串号。
    """
    if not client_order_no:
        return None
    return (db.query(Order)
              .filter(Order.client_order_no == client_order_no,
                      Order.store_id == scope.store_id)
              .first())
