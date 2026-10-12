"""shift —— 交班域。"""
from datetime import datetime

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy import func
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Order, Payment, Shift
from ..util import BizError, Ctx, get_ctx, ok

router = APIRouter(prefix="/api/v1/shift", tags=["shift"])


def _summary(db: Session, shift_id: int) -> dict:
    orders = db.query(Order).filter(Order.shift_id == shift_id).all()
    order_ids = [o.id for o in orders]
    cash = member = 0.0
    if order_ids:
        for p in db.query(Payment).filter(Payment.order_id.in_(order_ids),
                                          Payment.status == 1).all():
            if p.pay_type == "CASH":
                cash += float(p.amount or 0)
            elif p.pay_type == "MEMBER":
                member += float(p.amount or 0)
    settled = [o for o in orders if o.status == 1]
    return {
        "orderCount": len(orders),
        "settledCount": len(settled),
        "amountTotal": round(sum(float(o.amount_pay or 0) for o in settled), 2),
        "cashAmount": round(cash, 2),
        "memberAmount": round(member, 2),
        "refundAmount": round(sum(float(o.amount_refund or 0) for o in orders), 2),
    }


@router.post("/current")
def current(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s = (db.query(Shift)
           .filter(Shift.store_id == ctx.store_id, Shift.status == 0)
           .order_by(Shift.id.desc()).first())
    if not s:
        return ok(None)
    return ok({"shiftId": s.id, "terminalNo": s.terminal_no, "operatorId": s.operator_id,
               "startAt": s.start_at.strftime("%Y-%m-%d %H:%M:%S") if s.start_at else "",
               "cashOpen": float(s.cash_open or 0), **_summary(db, s.id)})


class OpenReq(BaseModel):
    cashOpen: float = 0


@router.post("/open")
def open_shift(req: OpenReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    exist = (db.query(Shift)
               .filter(Shift.store_id == ctx.store_id, Shift.status == 0).first())
    if exist:
        return ok({"shiftId": exist.id, "reused": True})
    s = Shift(store_id=ctx.store_id, terminal_no=ctx.terminal_no,
              operator_id=ctx.user_id, start_at=datetime.now(),
              cash_open=req.cashOpen, status=0)
    db.add(s)
    db.commit()
    return ok({"shiftId": s.id, "reused": False})


class CloseReq(BaseModel):
    cashActual: float = 0
    remark: str = ""


@router.post("/close")
def close_shift(req: CloseReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s = (db.query(Shift)
           .filter(Shift.store_id == ctx.store_id, Shift.status == 0)
           .order_by(Shift.id.desc()).first())
    if not s:
        raise BizError("404", "没有进行中的班次")
    sm = _summary(db, s.id)
    s.cash_should = round(float(s.cash_open or 0) + sm["cashAmount"], 2)
    s.cash_actual = req.cashActual
    s.cash_close = req.cashActual
    s.end_at = datetime.now()
    s.status = 1
    db.commit()
    return ok({"shiftId": s.id, "cashShould": float(s.cash_should),
               "cashActual": float(s.cash_actual),
               "diff": round(float(s.cash_actual) - float(s.cash_should), 2), **sm})


class PageReq(BaseModel):
    startTime: str = ""
    endTime: str = ""
    pageNumber: int = 1
    pageSize: int = 20


@router.post("/handoverList")
def handover_list(req: PageReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    q = db.query(Shift).filter(Shift.store_id == ctx.store_id, Shift.status == 1)
    if req.startTime:
        q = q.filter(Shift.start_at >= req.startTime)
    if req.endTime:
        q = q.filter(Shift.start_at <= req.endTime)
    total = q.count()
    rows = (q.order_by(Shift.id.desc())
             .offset((req.pageNumber - 1) * req.pageSize).limit(req.pageSize).all())
    out = []
    for s in rows:
        out.append({
            "shiftId": s.id, "terminalNo": s.terminal_no, "operatorId": s.operator_id,
            "startAt": s.start_at.strftime("%Y-%m-%d %H:%M:%S") if s.start_at else "",
            "endAt": s.end_at.strftime("%Y-%m-%d %H:%M:%S") if s.end_at else "",
            "cashOpen": float(s.cash_open or 0), "cashShould": float(s.cash_should or 0),
            "cashActual": float(s.cash_actual or 0), **_summary(db, s.id),
        })
    return ok({"total": total, "list": out})


class DetailReq(BaseModel):
    shiftId: int


@router.post("/detail")
def detail(req: DetailReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    s = db.get(Shift, req.shiftId)
    if not s or s.store_id != ctx.store_id:
        return ok(None)
    return ok({"shiftId": s.id, "cashOpen": float(s.cash_open or 0),
               "cashShould": float(s.cash_should or 0),
               "cashActual": float(s.cash_actual or 0), **_summary(db, s.id)})
