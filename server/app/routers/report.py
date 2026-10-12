"""report —— 报表域。"""
from datetime import datetime, timedelta

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy import func
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Order, OrderItem, Payment
from ..util import Ctx, get_ctx, ok

router = APIRouter(prefix="/api/v1/report", tags=["report"])


class RangeReq(BaseModel):
    startDate: str = ""
    endDate: str = ""
    days: int = 7


def _range(req: RangeReq):
    if req.startDate and req.endDate:
        return req.startDate + " 00:00:00", req.endDate + " 23:59:59"
    end = datetime.now()
    start = end - timedelta(days=max(1, req.days))
    return start.strftime("%Y-%m-%d 00:00:00"), end.strftime("%Y-%m-%d 23:59:59")


@router.post("/summary")
def summary(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    base = db.query(Order).filter(Order.store_id == ctx.store_id,
                                  Order.created_at >= s, Order.created_at <= e)
    settled = base.filter(Order.status == 1).all()
    all_orders = base.all()
    pay_total = round(sum(float(o.amount_pay or 0) for o in settled), 2)
    disc_total = round(sum(float(o.amount_discount or 0) for o in all_orders), 2)
    refund_total = round(sum(float(o.amount_refund or 0) for o in all_orders), 2)
    return ok({
        "startDate": s, "endDate": e,
        "orderCount": len(all_orders), "settledCount": len(settled),
        "amountPay": pay_total, "amountDiscount": disc_total, "amountRefund": refund_total,
        "amountNet": round(pay_total - refund_total, 2),
        "avgOrder": round(pay_total / len(settled), 2) if settled else 0,
    })


@router.post("/salesTrend")
def sales_trend(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    rows = (db.query(func.date(Order.created_at).label("d"),
                     func.count(Order.id), func.coalesce(func.sum(Order.amount_pay), 0))
              .filter(Order.store_id == ctx.store_id, Order.status == 1,
                      Order.created_at >= s, Order.created_at <= e)
              .group_by("d").order_by("d").all())
    return ok([{"date": str(r[0]), "orderCount": int(r[1]), "amount": float(r[2] or 0)}
               for r in rows])


@router.post("/payType")
def pay_type(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    rows = (db.query(Payment.pay_type, func.count(Payment.id),
                     func.coalesce(func.sum(Payment.amount), 0))
              .join(Order, Order.id == Payment.order_id)
              .filter(Order.store_id == ctx.store_id, Payment.status == 1,
                      Order.created_at >= s, Order.created_at <= e)
              .group_by(Payment.pay_type).all())
    total = sum(float(r[2] or 0) for r in rows) or 1
    return ok([{"payType": r[0], "count": int(r[1]), "amount": float(r[2] or 0),
                "ratio": round(float(r[2] or 0) / total * 100, 2)} for r in rows])


@router.post("/orderSource")
def order_source(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    rows = (db.query(Order.source, func.count(Order.id), func.coalesce(func.sum(Order.amount_pay), 0))
              .filter(Order.store_id == ctx.store_id, Order.status == 1,
                      Order.created_at >= s, Order.created_at <= e)
              .group_by(Order.source).all())
    name = {1: "POS", 2: "安卓", 3: "小程序"}
    return ok([{"source": int(r[0]), "sourceName": name.get(int(r[0]), str(r[0])),
                "count": int(r[1]), "amount": float(r[2] or 0)} for r in rows])


@router.post("/discount")
def discount(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    rows = (db.query(func.coalesce(func.sum(Order.amount_discount), 0),
                     func.count(Order.id))
              .filter(Order.store_id == ctx.store_id,
                      Order.created_at >= s, Order.created_at <= e).first())
    return ok({"amountDiscount": float(rows[0] or 0), "orderCount": int(rows[1] or 0)})


@router.post("/productRank")
def product_rank(req: RangeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s, e = _range(req)
    rows = (db.query(OrderItem.product_name, func.coalesce(func.sum(OrderItem.qty), 0),
                     func.coalesce(func.sum(OrderItem.amount), 0))
              .join(Order, Order.id == OrderItem.order_id)
              .filter(Order.store_id == ctx.store_id, Order.status == 1,
                      Order.created_at >= s, Order.created_at <= e)
              .group_by(OrderItem.product_name)
              .order_by(func.coalesce(func.sum(OrderItem.amount), 0).desc())
              .limit(20).all())
    return ok([{"productName": r[0], "qty": float(r[1] or 0), "amount": float(r[2] or 0)}
               for r in rows])
