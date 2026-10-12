"""
legacy_order —— 参考系统客户端兼容层：桌台 / 订单 / 交班

所有接口都遵循真实抓包的两种约定：
  * 请求业务字段藏在 `data`（二次编码 JSON 字符串）里，平铺也支持；
  * 响应 `data` 同样是二次编码的 JSON 字符串。

幂等规则（写接口统一）：
  1. 报文带 `orderNo` 且该单在本门店已存在 → 走「改单/更新」（仅限待结单）；
  2. 否则带 `clientOrderNo` 且已存在 → **409 + 原单据完整信息**（离线补传重放）；
  3. 其余 → 新建。
  已结/已退/已作废的单据被重复提交时一律 409 + 原单据。
"""
import json
import uuid
from datetime import datetime
from typing import Dict

from fastapi import APIRouter, Depends, Request
from sqlalchemy.orm import Session

from ..db import get_db
from ..legacy_core import (STATUS_FROM_LEGACY, Scope, desk_dto, envelope, err, f,
                           find_idempotent, i, kv_set, load_order_extra, now_str,
                           order_detail, parse_payload, pay_name, q, resolve_scope, s,
                           save_order_extra, ts_str)
from ..models import (Desk, DeskArea, Member, MemberBalanceLog, Order, OrderFlow,
                      OrderItem, Payment, Product, Shift)
from ..util import gen_order_no, gen_trade_no

router = APIRouter(tags=["legacy-order"])

STATUS_PENDING, STATUS_SETTLED, STATUS_REFUNDED, STATUS_VOID = 0, 1, 2, 3

# 已经不能再改单的状态
_LOCKED = (STATUS_SETTLED, STATUS_REFUNDED, STATUS_VOID)

# 落库上限：t_order.client_order_no 是 VARCHAR(64)、remark 是 VARCHAR(255)
MAX_CLIENT_NO = 64
MAX_REMARK = 255


def _trunc(v) -> str:
    """备注超长按 255 截断 —— 为一条备注把整单打回，客户端要重来一遍。"""
    return (v or "")[:MAX_REMARK]


def _check_products(db: Session, scope: Scope, products: list):
    """明细校验，返回 (错误码, 错误信息)；通过则 (0, "")。"""
    for idx, p in enumerate(products or [], 1):
        price = f(p.get("prodPrice") if p.get("prodPrice") is not None
                  else p.get("prodUnitAmt"))
        qty = q(p.get("quantity"))
        amount = f(p.get("prodSalesAmt"))
        pid = i(p.get("productId"))
        if price < 0 or amount < 0:
            return 400, "第 %d 个菜品的金额不能为负数" % idx
        if qty <= 0:
            return 400, "第 %d 个菜品的数量必须大于 0（当前 %s）" % (idx, p.get("quantity"))
        if pid:
            product = db.get(Product, pid)
            if product is None:
                return 404, "商品不存在: productId=%d" % pid
            if product.tenant_id != scope.tenant_id:
                return 404, "商品不存在: productId=%d" % pid
    return 0, ""


def _check_client_no(client_no: str):
    """幂等键校验，返回错误信息；通过则返回空串。"""
    if not client_no:
        return "缺少 clientOrderNo / orderNo，无法保证幂等"
    if len(client_no) > MAX_CLIENT_NO:
        return "clientOrderNo 超长（%d > %d 字符）" % (len(client_no), MAX_CLIENT_NO)
    return ""


async def _read(request: Request):
    """读请求体 → (业务载荷, 元信息)。空体也安全。"""
    raw = b""
    try:
        raw = await request.body()
    except Exception:                                    # pragma: no cover - 防御
        pass
    if not raw:
        return {}, {}
    try:
        body = json.loads(raw)
    except ValueError:
        return {}, {}
    return parse_payload(body)


def _member_of(db: Session, scope: Scope, payload: dict):
    """按 memberNo / memberCardNo / phone 定位会员（本租户内）。"""
    no = s(payload.get("memberNo") or payload.get("memberCardNo") or payload.get("memberPhone"))
    if not no:
        return None
    return (db.query(Member)
              .filter(Member.tenant_id == scope.tenant_id, Member.phone == no)
              .first())


def _shift_now(db: Session, scope: Scope):
    return (db.query(Shift)
              .filter(Shift.store_id == scope.store_id, Shift.status == 0)
              .order_by(Shift.id.desc()).first())


def _detail(db: Session, o: Order, scope: Scope) -> dict:
    m = None
    if o.member_id:
        m = db.get(Member, o.member_id)
    return order_detail(db, o, scope,
                        member_no=(m.phone if m else ""),
                        member_balance=float(m.balance or 0) if m else 0)


def _find_order(db: Session, scope: Scope, payload: dict):
    """按 orderNo 优先、clientOrderNo 兜底定位订单，且必须属于本门店。"""
    order_no = s(payload.get("orderNo"))
    if order_no:
        o = (db.query(Order)
               .filter(Order.order_no == order_no, Order.store_id == scope.store_id)
               .first())
        if o is not None:
            return o
    cno = s(payload.get("clientOrderNo"))
    if cno:
        return find_idempotent(db, scope, cno)
    return None


def _bind_desk(db: Session, scope: Scope, o: Order, desk_id: int) -> None:
    """把订单挂到桌台（先释放旧桌台，避免一张单占两张台）。"""
    if o.desk_id and o.desk_id != desk_id:
        old = db.get(Desk, o.desk_id)
        if old is not None and old.store_id == scope.store_id and old.current_order_id == o.id:
            old.status, old.current_order_id = 0, 0
    if not desk_id:
        return
    d = db.get(Desk, desk_id)
    if d is not None and d.store_id == scope.store_id:
        d.status = 1
        d.current_order_id = o.id


def _release_desk(db: Session, scope: Scope, o: Order) -> None:
    if not o.desk_id:
        return
    d = db.get(Desk, o.desk_id)
    if d is not None and d.store_id == scope.store_id and d.current_order_id == o.id:
        d.status, d.current_order_id = 0, 0


def _replace_items(db: Session, o: Order, products: list) -> float:
    """整单替换明细（下单/改单共用），返回商品金额合计。"""
    db.query(OrderItem).filter(OrderItem.order_id == o.id).delete(synchronize_session=False)
    total = 0.0
    for p in products or []:
        price = f(p.get("prodPrice") if p.get("prodPrice") is not None else p.get("prodUnitAmt"))
        qty = q(p.get("quantity"))
        amt = f(p.get("prodSalesAmt"))
        if not amt:
            amt = round(price * qty, 2)
        disc = f(p.get("prodDiscAmt") if p.get("prodDiscAmt") is not None
                 else p.get("prodSubtotalDiscAmt"))
        refunded = i(p.get("refundStatus")) == 1
        db.add(OrderItem(
            order_id=o.id,
            product_id=i(p.get("productId")),
            product_name=s(p.get("productName")),
            spec_text=s(p.get("specName")),
            price=price, qty=qty, amount=amt, discount_amount=disc,
            gift_flag=1 if i(p.get("discFlag")) == 2 else 0,
            remark=s(p.get("memo")),
            status=1 if refunded else 0))
        if not refunded:
            total += amt
    return round(total, 2)


def _apply_payments(db: Session, o: Order, scope: Scope, flows: list):
    """按 payFlows 落支付流水 + 扣会员余额。返回 (实收, 错误信息)。"""
    db.query(Payment).filter(Payment.order_id == o.id).delete(synchronize_session=False)
    member = db.get(Member, o.member_id) if o.member_id else None
    paid = 0.0
    for fl in flows or []:
        name = pay_name(fl.get("pmSign") if fl.get("pmSign") not in (None, "")
                        else fl.get("payType"))
        amt = f(fl.get("paymentAmt") if fl.get("paymentAmt") is not None else fl.get("paidAmt"))
        if amt <= 0:
            continue
        if name == "MEMBER":
            m = member or _member_of(db, scope, fl)
            if m is None:
                return paid, "会员不存在，无法会员支付"
            if float(m.balance or 0) + 0.001 < amt:
                return paid, "会员余额不足：余额 %.2f，需支付 %.2f" % (float(m.balance or 0), amt)
            before = float(m.balance or 0)
            m.balance = round(before - amt, 2)
            member = m
            db.add(MemberBalanceLog(member_id=m.id, type=2, amount=-amt,
                                    balance_before=before, balance_after=float(m.balance),
                                    order_id=o.id, operator_id=scope.user_id))
        db.add(Payment(order_id=o.id, pay_type=name,
                       channel=s(fl.get("orderChannel")) or None,
                       amount=amt, status=1,
                       out_trade_no=s(fl.get("outTradeNo")) or gen_trade_no("P"),
                       pay_time=datetime.now(), operator_id=scope.user_id))
        paid += amt
    return round(paid, 2), ""


def _flow(db: Session, o: Order, action: str, frm: int, to: int, scope: Scope) -> None:
    db.add(OrderFlow(order_id=o.id, action=action, from_status=frm, to_status=to,
                     operator_id=scope.user_id))


# ====================================================================
#  P0-3 桌台
# ====================================================================

@router.post("/pos/desk/openDesk")
async def open_desk(request: Request, db: Session = Depends(get_db)):
    """开台。真实响应：{"orderDesk":{...},"surcharges":[]}（data 二次编码）

    真实服务端在这里就会「创建正餐订单，生成订单号…」，客户端拿到的 orderNo
    后续 submit / settle 都带着它，所以这里也要真的建单。
    """
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    desk_id = i(payload.get("deskId"))
    d = db.get(Desk, desk_id)
    if d is None or d.store_id != scope.store_id:
        return err(404, "桌台不存在")

    # 已开台 → 直接回原单（天然幂等，客户端重试不会开出两张单）
    if d.status == 1 and d.current_order_id:
        o = db.get(Order, d.current_order_id)
        if o is not None and o.status == STATUS_PENDING:
            return envelope({"orderDesk": desk_dto(db, d, scope, o), "surcharges": []})

    order_no = gen_order_no(scope.store_id)
    shift = _shift_now(db, scope)
    o = Order(tenant_id=scope.tenant_id, store_id=scope.store_id,
              order_no=order_no, client_order_no=order_no,
              desk_id=desk_id, order_type=1, source=1,
              status=STATUS_PENDING, member_id=0,
              amount_total=0, amount_discount=0, order_discount=0, amount_pay=0,
              shift_id=shift.id if shift else 0, operator_id=scope.user_id,
              remark=_trunc(s(payload.get("memo"))))
    db.add(o)
    db.flush()

    d.status = 1
    d.current_order_id = o.id
    save_order_extra(db, o, {
        "orderDeskNo": uuid.uuid4().hex,
        "cashierId": s(payload.get("cashierId")),
        "cashierName": s(payload.get("cashierName")),
        "customerNum": i(payload.get("customerNum")),
        "childrenNum": i(payload.get("childNum")),
    })
    _flow(db, o, "openDesk", -1, STATUS_PENDING, scope)
    db.commit()

    od = desk_dto(db, d, scope, o)
    od["customerNum"] = i(payload.get("customerNum"))
    od["childrenNum"] = i(payload.get("childNum"))
    return envelope({"orderDesk": od, "surcharges": []})


@router.post("/pos/desk/transferDesk")
async def transfer_desk(request: Request, db: Session = Depends(get_db)):
    """换台。真实响应：{"orderDesks":[...],"surcharges":[],"returnCode":1}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    to_id = i(payload.get("toDeskId"))
    to = db.get(Desk, to_id)
    if to is None or to.store_id != scope.store_id:
        return err(404, "目标桌台不存在")
    if to.status == 1 and to.current_order_id:
        return err(409, "目标桌台已被占用")

    o = _find_order(db, scope, payload)
    if o is None:
        from_id = i(payload.get("fromDeskId"))
        d = db.get(Desk, from_id)
        if d is not None and d.current_order_id:
            o = db.get(Order, d.current_order_id)
    if o is None:
        return err(404, "订单不存在")
    if o.status != STATUS_PENDING:
        return err(409, "订单已结账，不能换台", _detail(db, o, scope))

    _bind_desk(db, scope, o, to_id)
    o.desk_id = to_id
    _flow(db, o, "transferDesk", o.status, o.status, scope)
    db.commit()
    return envelope({"orderDesks": [desk_dto(db, to, scope, o)], "surcharges": [],
                     "returnCode": 1})


@router.post("/pos/desk/clearDesk")
async def clear_desk(request: Request, db: Session = Depends(get_db)):
    """清台。真实响应：{"order":...,"orderDesks":[...],"surcharges":[...]}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    o = _find_order(db, scope, payload)
    desk_id = i(payload.get("deskId"))
    d = db.get(Desk, desk_id) if desk_id else None
    if o is None and d is not None and d.current_order_id:
        o = db.get(Order, d.current_order_id)
    if d is None and o is not None and o.desk_id:
        d = db.get(Desk, o.desk_id)
    if d is None or d.store_id != scope.store_id:
        return err(404, "桌台不存在")

    if o is not None and o.status == STATUS_PENDING:
        # 未结账就清台 → 作废该单，避免留下悬空单
        o.status = STATUS_VOID
        _flow(db, o, "clearDesk", STATUS_PENDING, STATUS_VOID, scope)
    d.status, d.current_order_id = 0, 0
    db.commit()

    od = desk_dto(db, d, scope, None)
    od["deskStatus"] = 1                       # 清台后回到「空闲」
    return envelope({
        "order": _detail(db, o, scope) if o is not None else None,
        "orderDesks": [od], "surcharges": [],
    })


@router.post("/pos/area/saveStoreDesk")
async def save_store_desk(request: Request, db: Session = Depends(get_db)):
    """新增/保存桌台。真实响应：{"id","areaId","areaName","deskName","seatsNum",
    "storeId","tenantId","orderDeskNo","codeList":[...]}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    area_id = i(payload.get("areaId"))
    area = db.get(DeskArea, area_id)
    if area is None or area.store_id != scope.store_id:
        return err(404, "区域不存在")

    name = s(payload.get("deskName"))
    seats = i(payload.get("seatsNum")) or 4
    desk = (db.query(Desk)
              .filter(Desk.store_id == scope.store_id, Desk.area_id == area_id,
                      Desk.name == name).first())
    if desk is None:
        desk = Desk(store_id=scope.store_id, area_id=area_id, name=name,
                    capacity=seats, status=0, current_order_id=0)
        db.add(desk)
        db.flush()
    else:
        desk.capacity = seats

    codes = payload.get("qrCodes") or []
    if isinstance(codes, str):
        codes = [x for x in codes.split(",") if x]
    db.commit()
    return envelope({
        "id": str(desk.id), "deskId": str(desk.id), "deskName": desk.name,
        "areaId": str(area.id), "areaName": area.name,
        "seatsNum": desk.capacity, "storeId": str(scope.store_id),
        "tenantId": str(scope.tenant_id), "orderDeskNo": "",
        "codeList": [{"qrCode": s(c)} for c in codes],
    })


# ====================================================================
#  P0-4 订单
# ====================================================================

@router.post("/pos/order/submit")
async def order_submit(request: Request, db: Session = Depends(get_db)):
    """下单 / 改单。请求体就是一个完整 OrderDetail。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    order_no_in = s(payload.get("orderNo"))
    client_no = s(payload.get("clientOrderNo")) or order_no_in
    bad = _check_client_no(client_no)
    if bad:
        return err(400, bad)

    o = None
    if order_no_in:
        o = (db.query(Order)
               .filter(Order.order_no == order_no_in, Order.store_id == scope.store_id)
               .first())
    if o is None:
        o = find_idempotent(db, scope, client_no)
        if o is not None:
            # 纯重放：同一 clientOrderNo 再来一次 → 409 + 原单据完整信息
            return err(409, "单据已存在", _detail(db, o, scope))
    if o is not None and o.status in _LOCKED:
        return err(409, "订单已结账/已退款/已作废，不能改单", _detail(db, o, scope))

    products = payload.get("products") or []
    code, msg = _check_products(db, scope, products)
    if code:
        return err(code, msg)
    total = _replace_items(db, o, products) if o is not None else 0.0

    if o is None:
        shift = _shift_now(db, scope)
        o = Order(tenant_id=scope.tenant_id, store_id=scope.store_id,
                  order_no=order_no_in or gen_order_no(scope.store_id),
                  client_order_no=client_no,
                  desk_id=i(payload.get("deskId")), order_type=i(payload.get("orderType")) or 1,
                  source=i(payload.get("orderSource")) or 1,
                  status=STATUS_PENDING, member_id=0,
                  amount_total=0, amount_discount=0, order_discount=0, amount_pay=0,
                  shift_id=shift.id if shift else 0, operator_id=scope.user_id,
                  remark=_trunc(s(payload.get("memo"))))
        db.add(o)
        db.flush()
        total = _replace_items(db, o, products)
        _flow(db, o, "submit", -1, STATUS_PENDING, scope)
    else:
        _flow(db, o, "change", o.status, o.status, scope)

    m = _member_of(db, scope, payload)
    o.member_id = m.id if m else 0
    o.desk_id = i(payload.get("deskId")) or o.desk_id
    o.remark = _trunc(s(payload.get("memo"))) or o.remark
    o.amount_total = total
    o.order_discount = f(payload.get("discStoreAmt"))
    o.amount_discount = round(o.order_discount + f(payload.get("discAmt")), 2)
    o.amount_pay = round(total - float(o.amount_discount), 2)

    extra = load_order_extra(db, o)
    extra.update({
        "discounts": payload.get("discounts") or [],
        "surcharges": payload.get("surcharges") or [],
        "coupons": payload.get("coupons") or [],
        "batches": payload.get("batches") or [],
        "channelOrderNo": s(payload.get("channelOrderNo")),
        "orderDeskNo": extra.get("orderDeskNo") or uuid.uuid4().hex,
    })
    diners = i(payload.get("customerNum") or payload.get("numberOfDiners"))
    if diners:
        extra["customerNum"] = diners
    save_order_extra(db, o, extra)
    _bind_desk(db, scope, o, o.desk_id)
    db.commit()
    return envelope(_detail(db, o, scope))


@router.post("/pos/order/preSettle")
async def order_pre_settle(request: Request, db: Session = Depends(get_db)):
    """预结账：算钱不落库，返回带应付金额的 OrderDetail。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    o = _find_order(db, scope, payload)
    if o is None:
        return err(404, "订单不存在")

    detail = _detail(db, o, scope)
    m = _member_of(db, scope, payload)
    if m is not None:
        detail["memberNo"] = m.phone
        detail["memberBalance"] = float(m.balance or 0)
    detail["userReceiveAmt"] = detail["amt"] - detail["discAmt"]
    detail["receiveAmt"] = detail["userReceiveAmt"]
    return envelope(detail)


@router.post("/pos/order/settle")
@router.post("/pos/order/settleIdempotent")
async def order_settle(request: Request, db: Session = Depends(get_db)):
    """结账。真实响应：data 解码后是 {"orderDetail": {...}}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    order_no_in = s(payload.get("orderNo"))
    client_no = s(payload.get("clientOrderNo")) or order_no_in
    o = _find_order(db, scope, payload)
    if o is None:
        bad = _check_client_no(client_no)
        if bad:
            return err(404, "订单不存在") if not client_no else err(400, bad)
        code, msg = _check_products(db, scope, payload.get("products") or [])
        if code:
            return err(code, msg)
        # 离线补传：服务端没这单，但客户端已经结了 → 先落单再结
        shift = _shift_now(db, scope)
        o = Order(tenant_id=scope.tenant_id, store_id=scope.store_id,
                  order_no=order_no_in or gen_order_no(scope.store_id),
                  client_order_no=client_no,
                  desk_id=i(payload.get("deskId")), order_type=i(payload.get("orderType")) or 1,
                  source=i(payload.get("orderSource")) or 1,
                  status=STATUS_PENDING, member_id=0,
                  amount_total=0, amount_discount=0, order_discount=0, amount_pay=0,
                  shift_id=shift.id if shift else 0, operator_id=scope.user_id,
                  remark=_trunc(s(payload.get("memo"))))
        db.add(o)
        db.flush()
        total = _replace_items(db, o, payload.get("products") or [])
        m0 = _member_of(db, scope, payload)
        o.member_id = m0.id if m0 else 0
        o.amount_total = total
        o.amount_discount = round(f(payload.get("discStoreAmt")) + f(payload.get("discAmt")), 2)
        o.amount_pay = round(total - float(o.amount_discount), 2)
        _flow(db, o, "submit", -1, STATUS_PENDING, scope)

    if o.status == STATUS_SETTLED:
        # 重复结账 → 409 + 原单据（客户端把 409 当成功）
        return err(409, "订单已结账", {"orderDetail": _detail(db, o, scope)})
    if o.status != STATUS_PENDING:
        return err(409, "订单状态不允许结账", {"orderDetail": _detail(db, o, scope)})

    # 先用条件更新抢占结账权，避免并发请求同时写支付流水/扣会员余额。
    if o.id is None:
        db.flush()
    order_id = o.id
    claimed = (db.query(Order)
                 .filter(Order.id == order_id, Order.store_id == scope.store_id,
                         Order.status == STATUS_PENDING)
                 .update({Order.status: STATUS_SETTLED}, synchronize_session=False))
    if claimed != 1:
        db.rollback()
        current = db.get(Order, order_id)
        data = {"orderDetail": _detail(db, current, scope)} if current else None
        return err(409, "订单已结账", data)
    # 更新语句执行后丢弃之前读到的 ORM 状态，后续属性写入仍保留条件更新状态。
    db.expire(o)
    o.status = STATUS_SETTLED

    m = _member_of(db, scope, payload)
    if m is not None:
        o.member_id = m.id

    paid, msg = _apply_payments(db, o, scope, payload.get("payFlows") or [])
    if msg:
        db.rollback()
        return err(409, msg)
    if round(paid, 2) + 0.001 < round(float(o.amount_pay or 0), 2):
        db.rollback()
        return err(409, "支付金额不足：应付 %.2f，实付 %.2f" % (float(o.amount_pay or 0), paid))

    o.status = STATUS_SETTLED
    o.settle_time = datetime.now()
    _flow(db, o, "settle", STATUS_PENDING, STATUS_SETTLED, scope)
    _release_desk(db, scope, o)
    db.commit()

    detail = _detail(db, o, scope)
    detail["paidAmt"] = paid
    detail["receiveAmt"] = paid
    detail["userReceiveAmt"] = paid
    detail["payStatus"] = 1
    return envelope({"orderDetail": detail})


@router.post("/pos/order/detail")
async def order_detail_api(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    o = _find_order(db, scope, payload)
    if o is None:
        return err(404, "订单不存在")
    return envelope(_detail(db, o, scope))


@router.post("/pos/order/cancel")
@router.post("/pos/order/orderCancel")
async def order_cancel(request: Request, db: Session = Depends(get_db)):
    """撤单。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    o = _find_order(db, scope, payload)
    if o is None:
        return err(404, "订单不存在")
    if o.status == STATUS_SETTLED:
        return err(409, "已结账订单请走退单流程", _detail(db, o, scope))
    if o.status == STATUS_VOID:
        return err(409, "订单已撤单", _detail(db, o, scope))

    o.status = STATUS_VOID
    _flow(db, o, "cancel", STATUS_PENDING, STATUS_VOID, scope)
    _release_desk(db, scope, o)
    db.commit()
    return envelope(_detail(db, o, scope))


@router.post("/pos/order/refund")
@router.post("/pos/order/refundProduct")
async def order_refund(request: Request, db: Session = Depends(get_db)):
    """退单（整单退）。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    o = _find_order(db, scope, payload)
    if o is None:
        return err(404, "订单不存在")
    if o.status == STATUS_REFUNDED:
        return err(409, "订单已退单", _detail(db, o, scope))
    if o.status != STATUS_SETTLED:
        return err(409, "仅已结账订单可退", _detail(db, o, scope))

    refundable = round(float(o.amount_pay or 0) - float(o.amount_refund or 0), 2)
    amount = f(payload.get("refundAmt") or payload.get("paidAmt")) or refundable
    if amount > refundable + 0.001:
        return err(409, "退款金额超出可退余额 %.2f" % refundable)

    prev_refund = float(o.amount_refund or 0)
    is_full = amount >= refundable - 0.001

    pays = db.query(Payment).filter(Payment.order_id == o.id).all()
    member = db.get(Member, o.member_id) if o.member_id else None
    # 会员卡只退「会员实付的那部分」，且已退过的部分不再重复退，
    # 否则多次部分退款会把卡里余额退超。
    member_paid = sum(float(p.amount or 0) for p in pays
                      if pay_name(p.pay_type) == "MEMBER")
    member_refund = max(0.0, round(min(amount, member_paid - min(prev_refund, member_paid)), 2))
    if member is not None and member_refund > 0:
        before = float(member.balance or 0)
        member.balance = round(before + member_refund, 2)
        db.add(MemberBalanceLog(member_id=member.id, type=3, amount=member_refund,
                                balance_before=before, balance_after=float(member.balance),
                                order_id=o.id, operator_id=scope.user_id))

    # 只有整单退才把流水置为「已退」（status=3）。部分退款保留流水，
    # 交班时按 amount_refund 从钱箱里扣，否则同一笔退款会扣两遍。
    if is_full:
        for p in pays:
            p.status = 3

    # 退掉的菜品打上已退标记
    if payload.get("products") or payload.get("refundProducts"):
        names = {s(x.get("productName")) for x in (payload.get("products")
                                                  or payload.get("refundProducts"))}
        for it in db.query(OrderItem).filter(OrderItem.order_id == o.id).all():
            if it.product_name in names:
                it.status = 1

    o.amount_refund = round(float(o.amount_refund or 0) + amount, 2)
    if o.amount_refund >= float(o.amount_pay or 0) - 0.001:
        o.status = STATUS_REFUNDED
    _flow(db, o, "refund", STATUS_SETTLED, o.status, scope)
    db.commit()
    return envelope(_detail(db, o, scope))


@router.post("/pos/order/reverseSettle")
async def order_reverse_settle(request: Request, db: Session = Depends(get_db)):
    """反结账。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    o = _find_order(db, scope, payload)
    if o is None:
        return err(404, "订单不存在")
    if o.status != STATUS_SETTLED:
        return err(409, "仅已结账订单可反结账", _detail(db, o, scope))

    member = db.get(Member, o.member_id) if o.member_id else None
    for p in db.query(Payment).filter(Payment.order_id == o.id).all():
        if pay_name(p.pay_type) == "MEMBER" and member is not None and int(p.status) == 1:
            before = float(member.balance or 0)
            member.balance = round(before + float(p.amount or 0), 2)
            db.add(MemberBalanceLog(member_id=member.id, type=3, amount=float(p.amount or 0),
                                    balance_before=before, balance_after=float(member.balance),
                                    order_id=o.id, operator_id=scope.user_id))
        p.status = 3

    o.status = STATUS_PENDING
    o.settle_time = None
    o.amount_refund = 0
    _flow(db, o, "reverseSettle", STATUS_SETTLED, STATUS_PENDING, scope)
    _bind_desk(db, scope, o, o.desk_id)
    db.commit()
    return envelope(_detail(db, o, scope))


@router.post("/pos/order/page")
@router.post("/pos/order/queryOrder")
async def order_page(request: Request, db: Session = Depends(get_db)):
    """订单分页。响应：{total, pages, lastPage, pageNum, pageSize, records:[OrderDetail]}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    page_num = i(payload.get("pageNum") or payload.get("pageNumber")) or 1
    page_size = i(payload.get("pageSize")) or 20
    status_in = i(payload.get("orderStatus"), -1)

    qy = db.query(Order).filter(Order.store_id == scope.store_id)
    if status_in > 0:
        qy = qy.filter(Order.status == STATUS_FROM_LEGACY.get(status_in, -1))
    cno = s(payload.get("clientOrderNo"))
    if cno:
        qy = qy.filter(Order.client_order_no == cno)
    if s(payload.get("orderNo")):
        qy = qy.filter(Order.order_no.like("%" + s(payload.get("orderNo")) + "%"))
    if s(payload.get("beginTime")):
        qy = qy.filter(Order.created_at >= s(payload.get("beginTime")))
    if s(payload.get("endTime")):
        qy = qy.filter(Order.created_at <= s(payload.get("endTime")))
    if i(payload.get("deskId")):
        qy = qy.filter(Order.desk_id == i(payload.get("deskId")))

    total = qy.count()
    rows = (qy.order_by(Order.id.desc())
              .offset((page_num - 1) * page_size).limit(page_size).all())
    pages = (total + page_size - 1) // page_size if page_size else 0
    return envelope({
        "total": total, "pages": pages, "lastPage": page_num >= pages,
        "pageNum": page_num, "pageSize": page_size,
        "records": [_detail(db, o, scope) for o in rows],
    })


# ====================================================================
#  P0-5 交班
# ====================================================================

def _shift_summary(db: Session, shift: Shift) -> dict:
    qy = db.query(Order).filter(Order.store_id == shift.store_id, Order.shift_id == shift.id)
    orders = qy.all()
    settled = [o for o in orders if int(o.status or 0) == STATUS_SETTLED]
    cash = member_amt = 0.0
    cash_refund = member_refund = 0.0
    if settled:
        ids = [o.id for o in settled]
        # status 1 / 3 都算「收进来过」：整单退之后流水会翻成 3，但钱当时
        # 确实进过钱箱，退款额在下面单独减，否则同一笔退款会扣两遍。
        by_order: Dict[int, Dict[str, float]] = {}
        for p in db.query(Payment).filter(Payment.order_id.in_(ids),
                                          Payment.status.in_((1, 3))).all():
            name = pay_name(p.pay_type)
            if name not in ("CASH", "MEMBER"):
                continue
            slot = by_order.setdefault(int(p.order_id), {"CASH": 0.0, "MEMBER": 0.0})
            slot[name] += float(p.amount or 0)
        for o in settled:
            slot = by_order.get(int(o.id), {"CASH": 0.0, "MEMBER": 0.0})
            cash += slot["CASH"]
            member_amt += slot["MEMBER"]
            r = float(o.amount_refund or 0)
            if r <= 0:
                continue
            paid = slot["CASH"] + slot["MEMBER"]
            if paid <= 0:
                continue
            # 退款按收款构成比例拆：退到现金的那部分才从钱箱扣，
            # 退到会员卡的那部分余额已经还回卡里，不能再动现金。
            back_to_cash = round(r * slot["CASH"] / paid, 2)
            cash_refund += back_to_cash
            member_refund += r - back_to_cash
    refund = sum(float(o.amount_refund or 0) for o in orders)
    return {
        "shiftId": str(shift.id),
        "shiftNo": "S%06d" % shift.id,
        "terminalNo": shift.terminal_no or "",
        "orderCount": len(orders),
        "settledCount": len(settled),
        "amountTotal": round(sum(float(o.amount_total or 0) for o in orders), 2),
        "cashAmount": round(cash, 2),
        "memberAmount": round(member_amt - member_refund, 2),
        "refundAmount": round(refund, 2),
        "cashOpen": float(shift.cash_open or 0),
        "cashShould": round(float(shift.cash_open or 0) + cash - cash_refund, 2),
        "cashActual": float(shift.cash_actual or 0),
        "status": int(shift.status or 0),
        "startAt": ts_str(shift.start_at),
        "endAt": ts_str(shift.end_at),
    }


@router.post("/pos/shift/open")
async def shift_open(request: Request, db: Session = Depends(get_db)):
    """开班。已有进行中的班次则直接返回（幂等）。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    cur = _shift_now(db, scope)
    if cur is not None:
        return envelope(_shift_summary(db, cur))

    sh = Shift(store_id=scope.store_id, terminal_no=scope.terminal_no,
               operator_id=scope.user_id, start_at=datetime.now(),
               cash_open=f(payload.get("cashOpen") or payload.get("cashAmount")),
               cash_close=0, cash_should=0, cash_actual=0, status=0)
    db.add(sh)
    db.commit()
    return envelope(_shift_summary(db, sh))


@router.post("/pos/shift/close")
async def shift_close(request: Request, db: Session = Depends(get_db)):
    """交班。"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    sh = None
    if i(payload.get("shiftId")):
        sh = db.get(Shift, i(payload.get("shiftId")))
        if sh is not None and sh.store_id != scope.store_id:
            sh = None
    if sh is None:
        sh = _shift_now(db, scope)
    if sh is None:
        # 没有进行中的班次：本门店若已交过班，按幂等语义回 409 + 原班次单据；
        # 从未开过班才回 404，客户端可据此区分「重复交班」与「无班次」。
        last = (db.query(Shift).filter(Shift.store_id == scope.store_id)
                  .order_by(Shift.id.desc()).first())
        if last is None:
            return err(404, "没有进行中的班次")
        return err(409, "该班次已交班", _shift_summary(db, last))
    if sh.status == 1:
        return err(409, "该班次已交班", _shift_summary(db, sh))

    sh.cash_actual = f(payload.get("cashActual") or payload.get("cashClose"))
    sh.status = 1
    sh.end_at = datetime.now()
    db.flush()
    summary = _shift_summary(db, sh)
    sh.cash_should = summary["cashShould"]
    db.commit()
    return envelope(summary)


@router.post("/pos/handover/getHandoverList")
async def handover_list(request: Request, db: Session = Depends(get_db)):
    """交接记录。真实响应：{lastPage,pageNum,pageSize,pages,records:[{handoverBeginTime,
    handoverNo,handoverTime,terminalNo,workName,workNo}]}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)

    page_num = i(payload.get("pageNum")) or 1
    page_size = i(payload.get("pageSize")) or 20

    qy = db.query(Shift).filter(Shift.store_id == scope.store_id, Shift.status == 1)
    if s(payload.get("beginTime")):
        qy = qy.filter(Shift.end_at >= s(payload.get("beginTime")))
    if s(payload.get("endTime")):
        qy = qy.filter(Shift.end_at <= s(payload.get("endTime")))

    total = qy.count()
    rows = (qy.order_by(Shift.id.desc())
              .offset((page_num - 1) * page_size).limit(page_size).all())
    pages = (total + page_size - 1) // page_size if page_size else 0
    records = [{
        "handoverNo": "S%06d" % x.id,
        "handoverBeginTime": ts_str(x.start_at),
        "handoverTime": ts_str(x.end_at),
        "terminalNo": x.terminal_no or "",
        "workNo": str(x.operator_id or ""),
        "workName": "",
    } for x in rows]
    return envelope({"total": total, "pages": pages, "lastPage": page_num >= pages,
                     "pageNum": page_num, "pageSize": page_size, "records": records})


@router.post("/pos/upload/shiftticketV1")
async def upload_shift_ticket(request: Request, db: Session = Depends(get_db)):
    """交班小票上传。真实响应**没有 data 字段**：{key_sign, success, trace_no}"""
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    raw = await request.body()
    key = "shiftticket:" + (s(payload.get("shiftNo") or payload.get("handoverNo"))
                            or datetime.now().strftime("%Y%m%d%H%M%S"))
    kv_set(db, scope.tenant_id, scope.store_id, key[:64],
           {"raw": raw.decode("utf-8", "replace")[:20000], "at": now_str()})
    db.commit()
    return {"key_sign": "", "success": True, "trace_no": uuid.uuid4().hex}
