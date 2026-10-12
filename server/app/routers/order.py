"""order —— 订单域（核心）。"""
from datetime import datetime
from typing import List, Optional

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy import func
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import (Desk, Member, MemberBalanceLog, Order, OrderFlow, OrderItem,
                      Payment, Product, Shift)
from ..util import BizError, Ctx, gen_order_no, gen_trade_no, get_ctx, ok

router = APIRouter(prefix="/api/v1/order", tags=["order"])

STATUS_PENDING, STATUS_SETTLED, STATUS_REFUNDED, STATUS_VOID = 0, 1, 2, 3


class ItemIn(BaseModel):
    productId: Optional[int] = None
    productName: str = ""
    specText: str = ""
    price: float = 0
    qty: float = 1
    discountAmount: float = 0
    giftFlag: int = 0
    remark: str = ""


class CreateReq(BaseModel):
    clientOrderNo: str
    deskId: int = 0
    orderType: int = 1
    memberId: int = 0
    remark: str = ""
    items: List[ItemIn] = []


def _calc(items: List[ItemIn]):
    total = 0.0
    disc = 0.0
    for it in items:
        amt = round(float(it.price) * float(it.qty), 2)
        total += amt
        disc += float(it.discountAmount or 0)
    return round(total, 2), round(disc, 2)


def _add_flow(db: Session, order_id: int, action: str, frm: int, to: int, operator_id: int):
    db.add(OrderFlow(order_id=order_id, action=action, from_status=frm, to_status=to,
                     operator_id=operator_id))


def _order_dto(db: Session, o: Order) -> dict:
    items = db.query(OrderItem).filter(OrderItem.order_id == o.id).all()
    pays = db.query(Payment).filter(Payment.order_id == o.id).all()
    return {
        "orderId": o.id, "orderNo": o.order_no, "clientOrderNo": o.client_order_no,
        "deskId": o.desk_id, "orderType": o.order_type, "source": o.source,
        "status": o.status, "memberId": o.member_id,
        "amountTotal": float(o.amount_total or 0),
        "amountDiscount": float(o.amount_discount or 0),
        "amountPay": float(o.amount_pay or 0),
        "amountRefund": float(o.amount_refund or 0),
        "remark": o.remark,
        "createdAt": o.created_at.strftime("%Y-%m-%d %H:%M:%S") if o.created_at else "",
        "settleTime": o.settle_time.strftime("%Y-%m-%d %H:%M:%S") if o.settle_time else "",
        "items": [{"id": i.id, "productId": i.product_id, "productName": i.product_name,
                   "specText": i.spec_text, "price": float(i.price or 0), "qty": float(i.qty or 0),
                   "amount": float(i.amount or 0), "discountAmount": float(i.discount_amount or 0),
                   "giftFlag": i.gift_flag, "remark": i.remark, "status": i.status} for i in items],
        "payments": [{"id": p.id, "payType": p.pay_type, "amount": float(p.amount or 0),
                      "status": p.status, "outTradeNo": p.out_trade_no,
                      "payTime": p.pay_time.strftime("%Y-%m-%d %H:%M:%S") if p.pay_time else ""}
                     for p in pays],
    }


@router.post("/create")
def create(req: CreateReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    # 幂等键必须带 store_id 过滤：不同门店可能出现相同的 clientOrderNo，
    # 不隔离会把别家门店的单据当成「重复提交」返回出去。
    exist = (db.query(Order)
               .filter(Order.client_order_no == req.clientOrderNo,
                       Order.store_id == ctx.store_id)
               .first())
    if exist:
        raise BizError("409", "单据已存在", _order_dto(db, exist))

    total, disc = _calc(req.items)
    shift = (db.query(Shift)
               .filter(Shift.store_id == ctx.store_id, Shift.status == 0)
               .order_by(Shift.id.desc()).first())

    o = Order(tenant_id=ctx.tenant_id, store_id=ctx.store_id,
              order_no=gen_order_no(ctx.store_id), client_order_no=req.clientOrderNo,
              desk_id=req.deskId, order_type=req.orderType, source=1,
              status=STATUS_PENDING, member_id=req.memberId,
              amount_total=total, amount_discount=disc,
              amount_pay=round(total - disc, 2),
              shift_id=shift.id if shift else 0, operator_id=ctx.user_id,
              remark=req.remark)
    try:
        db.add(o)
        db.flush()

        for it in req.items:
            pid = it.productId
            name = it.productName
            if pid and not name:
                p = db.get(Product, pid)
                name = p.name if p else ""
            db.add(OrderItem(order_id=o.id, product_id=pid or 0, product_name=name,
                             spec_text=it.specText, price=it.price, qty=it.qty,
                             amount=round(float(it.price) * float(it.qty), 2),
                             discount_amount=it.discountAmount, gift_flag=it.giftFlag,
                             remark=it.remark))

        if req.deskId:
            d = db.get(Desk, req.deskId)
            if d and d.store_id == ctx.store_id:
                d.status = 1
                d.current_order_id = o.id

        _add_flow(db, o.id, "create", -1, STATUS_PENDING, ctx.user_id)
        db.commit()
    except IntegrityError:
        # t_order.client_order_no 是「全局」唯一索引（不带 store_id，见 models.py），
        # 上面按门店过滤的查询挡不住跨门店撞号。真撞上时与其漏出 500 让客户端
        # 卡死，不如按「单据已存在」回 409 —— 客户端本来就把它当重复提交处理。
        db.rollback()
        other = (db.query(Order)
                   .filter(Order.client_order_no == req.clientOrderNo)
                   .first())
        raise BizError("409", "单据已存在",
                       _order_dto(db, other) if other else None)
    return ok(_order_dto(db, o))


class ChangeReq(BaseModel):
    orderId: int
    items: List[ItemIn] = []


@router.post("/change")
def change(req: ChangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status != STATUS_PENDING:
        raise BizError("409", "订单已结账，不能改单")
    # 简化策略：整单替换明细（真实场景应做增量 diff，保留已下单菜品状态）
    db.query(OrderItem).filter(OrderItem.order_id == o.id).delete(synchronize_session=False)
    for it in req.items:
        db.add(OrderItem(order_id=o.id, product_id=it.productId or 0, product_name=it.productName,
                         spec_text=it.specText, price=it.price, qty=it.qty,
                         amount=round(float(it.price) * float(it.qty), 2),
                         discount_amount=it.discountAmount, gift_flag=it.giftFlag,
                         remark=it.remark))
    o.amount_total, _ = _calc(req.items)
    _recalc_discount(db, o)
    _add_flow(db, o.id, "change", o.status, o.status, ctx.user_id)
    db.commit()
    return ok(_order_dto(db, o))


class DiscountReq(BaseModel):
    orderId: int
    target: str = "order"          # order | item
    itemId: Optional[int] = None
    type: str = "amount"           # amount | rate
    value: float = 0


def _recalc_discount(db: Session, o: Order) -> None:
    """整单折扣与单品折扣分开存，保证重复调用幂等。"""
    # 会话关掉了 autoflush（见 db.py），不显式 flush 的话刚改的单品行折扣
    # 查不到，SUM 出来是旧值 —— 单品折扣会「点了没反应」。
    db.flush()
    item_disc = float(db.query(func.coalesce(func.sum(OrderItem.discount_amount), 0))
                        .filter(OrderItem.order_id == o.id).scalar() or 0)
    o.amount_discount = round(float(o.order_discount or 0) + item_disc, 2)
    o.amount_pay = round(float(o.amount_total) - float(o.amount_discount), 2)


@router.post("/discount")
def discount(req: DiscountReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status != STATUS_PENDING:
        raise BizError("409", "订单已结账，不能改折扣")
    if req.target == "item" and req.itemId:
        it = db.get(OrderItem, req.itemId)
        if it and it.order_id == o.id:
            base = round(float(it.price) * float(it.qty), 2)
            it.discount_amount = round(base * (1 - req.value), 2) if req.type == "rate" else req.value
    else:
        if req.type == "rate":
            o.order_discount = round(float(o.amount_total) * (1 - req.value), 2)
        else:
            o.order_discount = req.value
    _recalc_discount(db, o)
    db.commit()
    return ok({"orderId": o.id, "amountDiscount": float(o.amount_discount),
               "amountPay": float(o.amount_pay)})


class PreSettleReq(BaseModel):
    orderId: int
    memberId: int = 0


@router.post("/preSettle")
def pre_settle(req: PreSettleReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    pay = round(float(o.amount_total) - float(o.amount_discount), 2)
    member = db.get(Member, req.memberId) if req.memberId else None
    return ok({"orderId": o.id, "amountTotal": float(o.amount_total),
               "amountDiscount": float(o.amount_discount), "amountPay": pay,
               "memberBalance": float(member.balance or 0) if member else 0,
               "items": _order_dto(db, o)["items"]})


class PayIn(BaseModel):
    payType: str
    amount: float
    channel: str = ""
    memberId: int = 0
    outTradeNo: str = ""


class SettleReq(BaseModel):
    clientOrderNo: str
    orderId: int
    payments: List[PayIn]
    remark: str = ""


@router.post("/settle")
def settle(req: SettleReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status == STATUS_SETTLED:
        raise BizError("409", "订单已结账", _order_dto(db, o))

    paid = 0.0
    for p in req.payments:
        if p.payType == "MEMBER" and p.memberId:
            m = db.get(Member, p.memberId)
            if not m:
                raise BizError("404", "会员不存在")
            if float(m.balance or 0) < p.amount:
                raise BizError("409", "会员余额不足")
            before = float(m.balance)
            m.balance = round(before - p.amount, 2)
            db.add(MemberBalanceLog(member_id=m.id, type=2, amount=-p.amount,
                                    balance_before=before, balance_after=float(m.balance),
                                    order_id=o.id, operator_id=ctx.user_id))
        db.add(Payment(order_id=o.id, pay_type=p.payType, channel=p.channel or None,
                       amount=p.amount, status=1,
                       out_trade_no=p.outTradeNo or gen_trade_no("P"),
                       pay_time=datetime.now(), operator_id=ctx.user_id))
        paid += p.amount

    if round(paid, 2) + 0.001 < round(float(o.amount_pay or 0), 2):
        db.rollback()
        raise BizError("409", "支付金额不足：应付 %.2f，实付 %.2f" % (float(o.amount_pay), paid))

    o.status = STATUS_SETTLED
    o.settle_time = datetime.now()
    o.remark = req.remark or o.remark
    _add_flow(db, o.id, "settle", STATUS_PENDING, STATUS_SETTLED, ctx.user_id)

    if o.desk_id:
        d = db.get(Desk, o.desk_id)
        if d:
            d.status, d.current_order_id = 0, 0
    db.commit()
    return ok({"orderId": o.id, "orderNo": o.order_no, "paid": round(paid, 2),
               "change": round(paid - float(o.amount_pay or 0), 2)})


class OrderIdReq(BaseModel):
    orderId: int


@router.post("/detail")
def detail(req: OrderIdReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        return ok(None)
    return ok(_order_dto(db, o))


class PageReq(BaseModel):
    startTime: str = ""
    endTime: str = ""
    status: Optional[int] = None
    orderNo: str = ""
    pageNumber: int = 1
    pageSize: int = 20


@router.post("/page")
def page(req: PageReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    q = db.query(Order).filter(Order.store_id == ctx.store_id)
    if req.status is not None:
        q = q.filter(Order.status == req.status)
    if req.orderNo:
        q = q.filter(Order.order_no.like("%%%s%%" % req.orderNo))
    if req.startTime:
        q = q.filter(Order.created_at >= req.startTime)
    if req.endTime:
        q = q.filter(Order.created_at <= req.endTime)
    total = q.count()
    rows = (q.order_by(Order.id.desc())
             .offset((req.pageNumber - 1) * req.pageSize).limit(req.pageSize).all())
    return ok({"total": total, "list": [{
        "orderId": o.id, "orderNo": o.order_no, "status": o.status,
        "deskId": o.desk_id, "amountPay": float(o.amount_pay or 0),
        "createdAt": o.created_at.strftime("%Y-%m-%d %H:%M:%S") if o.created_at else "",
    } for o in rows]})


@router.post("/cancel")
def cancel(req: OrderIdReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status == STATUS_SETTLED:
        raise BizError("409", "已结账订单请走退单流程")
    o.status = STATUS_VOID
    if o.desk_id:
        d = db.get(Desk, o.desk_id)
        if d and d.current_order_id == o.id:
            d.status, d.current_order_id = 0, 0
    _add_flow(db, o.id, "cancel", STATUS_PENDING, STATUS_VOID, ctx.user_id)
    db.commit()
    return ok({"orderId": o.id, "status": o.status})


class RefundReq(BaseModel):
    orderId: int
    amount: float = 0
    reason: str = ""


@router.post("/refund")
def refund(req: RefundReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status != STATUS_SETTLED:
        raise BizError("409", "仅已结账订单可退")
    amount = req.amount or float(o.amount_pay or 0)
    if amount > float(o.amount_pay or 0) - float(o.amount_refund or 0):
        raise BizError("409", "退款金额超出可退余额")

    for p in db.query(Payment).filter(Payment.order_id == o.id, Payment.status == 1).all():
        if p.pay_type == "MEMBER":
            m = db.query(Member).filter(Member.id == o.member_id).first()
            if m:
                before = float(m.balance)
                m.balance = round(before + amount, 2)
                db.add(MemberBalanceLog(member_id=m.id, type=3, amount=amount,
                                        balance_before=before, balance_after=float(m.balance),
                                        order_id=o.id, operator_id=ctx.user_id))
        p.status = 3
        break

    o.amount_refund = round(float(o.amount_refund or 0) + amount, 2)
    if o.amount_refund >= float(o.amount_pay or 0):
        o.status = STATUS_REFUNDED
    _add_flow(db, o.id, "refund", STATUS_SETTLED, o.status, ctx.user_id)
    db.commit()
    return ok({"orderId": o.id, "refund": amount, "amountRefund": float(o.amount_refund),
               "status": o.status})


class ReverseReq(BaseModel):
    orderId: int
    reason: str = ""


@router.post("/reverseSettle")
def reverse_settle(req: ReverseReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    if o.status != STATUS_SETTLED:
        raise BizError("409", "仅已结账订单可反结账")
    for p in db.query(Payment).filter(Payment.order_id == o.id).all():
        p.status = 3
    o.status = STATUS_PENDING
    o.settle_time = None
    o.amount_refund = float(o.amount_pay or 0)
    _add_flow(db, o.id, "reverseSettle", STATUS_SETTLED, STATUS_PENDING, ctx.user_id)
    db.commit()
    return ok({"orderId": o.id, "status": o.status})


class ActionReq(BaseModel):
    orderId: int
    remark: str = ""


@router.post("/take")
def take(req: ActionReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    return _flow(db, ctx, req.orderId, "take")


@router.post("/storeOut")
def store_out(req: ActionReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    return _flow(db, ctx, req.orderId, "storeOut")


@router.post("/deliveryStart")
def delivery_start(req: ActionReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    return _flow(db, ctx, req.orderId, "deliveryStart")


@router.post("/deliveryComplete")
def delivery_complete(req: ActionReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    return _flow(db, ctx, req.orderId, "deliveryComplete")


def _flow(db: Session, ctx: Ctx, order_id: int, action: str):
    o = db.get(Order, order_id)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    _add_flow(db, o.id, action, o.status, o.status, ctx.user_id)
    db.commit()
    return ok({"orderId": o.id, "action": action})
