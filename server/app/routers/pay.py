"""pay —— 支付域（M1 只做现金/会员/记账；扫码通道二期接）。"""
from datetime import datetime

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Order, Payment
from ..util import BizError, Ctx, gen_trade_no, get_ctx, ok

router = APIRouter(prefix="/api/v1/pay", tags=["pay"])

PAY_TYPES = [
    {"code": "CASH", "name": "现金", "online": False, "sort": 1},
    {"code": "MEMBER", "name": "会员余额", "online": False, "sort": 2},
    {"code": "QRCODE", "name": "扫码支付", "online": True, "sort": 3},
    {"code": "BANK", "name": "银行卡", "online": True, "sort": 4},
    {"code": "CUSTOM", "name": "自定义", "online": False, "sort": 9},
]


@router.post("/payTypeList")
def pay_type_list(ctx: Ctx = Depends(get_ctx)):
    return ok(PAY_TYPES)


class CreateReq(BaseModel):
    orderId: int
    payType: str
    amount: float
    channel: str = ""


@router.post("/create")
def create(req: CreateReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    o = db.get(Order, req.orderId)
    if not o or o.store_id != ctx.store_id:
        raise BizError("404", "订单不存在")
    p = Payment(order_id=o.id, pay_type=req.payType, channel=req.channel or None,
                amount=req.amount, status=0, out_trade_no=gen_trade_no("P"),
                operator_id=ctx.user_id)
    db.add(p)
    db.commit()
    # 在线通道二期接：这里直接返回待支付，客户端轮询 /query
    return ok({"paymentId": p.id, "outTradeNo": p.out_trade_no, "status": p.status,
               "needOnlinePay": req.payType in ("QRCODE", "BANK")})


class QueryReq(BaseModel):
    outTradeNo: str = ""
    paymentId: int = 0


@router.post("/query")
def query(req: QueryReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    p = None
    if req.paymentId:
        p = db.get(Payment, req.paymentId)
    elif req.outTradeNo:
        p = db.query(Payment).filter(Payment.out_trade_no == req.outTradeNo).first()
    if not p:
        return ok(None)
    return ok({"paymentId": p.id, "status": p.status, "amount": float(p.amount or 0),
               "channelTradeNo": p.channel_trade_no,
               "payTime": p.pay_time.strftime("%Y-%m-%d %H:%M:%S") if p.pay_time else ""})


class ConfirmReq(BaseModel):
    paymentId: int
    channelTradeNo: str = ""


@router.post("/cashConfirm")
def cash_confirm(req: ConfirmReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Payment, req.paymentId)
    if not p:
        raise BizError("404", "支付单不存在")
    p.status = 1
    p.pay_time = datetime.now()
    p.channel_trade_no = req.channelTradeNo or None
    db.commit()
    return ok({"paymentId": p.id, "status": p.status})


class CancelReq(BaseModel):
    paymentId: int


@router.post("/cancel")
def cancel(req: CancelReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Payment, req.paymentId)
    if not p:
        raise BizError("404", "支付单不存在")
    if p.status == 1:
        raise BizError("409", "已支付，请走退款")
    p.status = 2
    db.commit()
    return ok({"paymentId": p.id, "status": p.status})
