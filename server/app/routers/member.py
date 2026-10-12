"""member —— 会员域。"""
from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Member, MemberBalanceLog
from ..util import BizError, Ctx, get_ctx, ok

router = APIRouter(prefix="/api/v1/member", tags=["member"])


def _dto(m: Member) -> dict:
    return {"memberId": m.id, "phone": m.phone, "name": m.name, "levelId": m.level_id,
            "balance": float(m.balance or 0), "score": m.score or 0, "status": m.status}


class QueryReq(BaseModel):
    phone: str = ""
    memberId: int = 0


@router.post("/query")
def query(req: QueryReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    if req.memberId:
        m = db.get(Member, req.memberId)
    else:
        m = (db.query(Member)
               .filter(Member.tenant_id == ctx.tenant_id, Member.phone == req.phone).first())
    if not m or m.tenant_id != ctx.tenant_id:
        return ok(None)
    return ok(_dto(m))


class RegisterReq(BaseModel):
    phone: str
    name: str = ""


@router.post("/register")
def register(req: RegisterReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    exist = (db.query(Member)
               .filter(Member.tenant_id == ctx.tenant_id, Member.phone == req.phone).first())
    if exist:
        raise BizError("409", "手机号已注册", _dto(exist))
    m = Member(tenant_id=ctx.tenant_id, phone=req.phone, name=req.name or req.phone[-4:],
               balance=0, score=0, status=1)
    db.add(m)
    db.commit()
    return ok(_dto(m))


class RechargeReq(BaseModel):
    memberId: int
    amount: float
    payType: str = "CASH"
    giftAmount: float = 0


@router.post("/recharge")
def recharge(req: RechargeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    m = db.get(Member, req.memberId)
    if not m or m.tenant_id != ctx.tenant_id:
        raise BizError("404", "会员不存在")
    if req.amount <= 0:
        raise BizError("400", "充值金额必须大于 0")
    before = float(m.balance or 0)
    m.balance = round(before + req.amount + req.giftAmount, 2)
    db.add(MemberBalanceLog(member_id=m.id, type=1, amount=req.amount + req.giftAmount,
                            balance_before=before, balance_after=float(m.balance),
                            operator_id=ctx.user_id))
    db.commit()
    return ok({"memberId": m.id, "balance": float(m.balance),
               "giftAmount": req.giftAmount})


class ConsumeReq(BaseModel):
    memberId: int
    amount: float
    orderId: int = 0


@router.post("/consume")
def consume(req: ConsumeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    m = db.get(Member, req.memberId)
    if not m or m.tenant_id != ctx.tenant_id:
        raise BizError("404", "会员不存在")
    if float(m.balance or 0) < req.amount:
        raise BizError("409", "余额不足")
    before = float(m.balance)
    m.balance = round(before - req.amount, 2)
    db.add(MemberBalanceLog(member_id=m.id, type=2, amount=-req.amount,
                            balance_before=before, balance_after=float(m.balance),
                            order_id=req.orderId, operator_id=ctx.user_id))
    db.commit()
    return ok({"memberId": m.id, "balance": float(m.balance)})


class BalanceReq(BaseModel):
    memberId: int
    pageNumber: int = 1
    pageSize: int = 20


@router.post("/balance")
def balance(req: BalanceReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    q = db.query(MemberBalanceLog).filter(MemberBalanceLog.member_id == req.memberId)
    total = q.count()
    rows = (q.order_by(MemberBalanceLog.id.desc())
             .offset((req.pageNumber - 1) * req.pageSize).limit(req.pageSize).all())
    m = db.get(Member, req.memberId)
    return ok({"balance": float(m.balance or 0) if m else 0, "total": total,
               "list": [{"id": r.id, "type": r.type, "amount": float(r.amount or 0),
                         "balanceAfter": float(r.balance_after or 0),
                         "orderId": r.order_id,
                         "createdAt": r.created_at.strftime("%Y-%m-%d %H:%M:%S") if r.created_at else ""}
                        for r in rows]})


class ScoreReq(BaseModel):
    memberId: int
    score: int = 0
    reason: str = ""


@router.post("/score")
def score(req: ScoreReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    m = db.get(Member, req.memberId)
    if not m or m.tenant_id != ctx.tenant_id:
        raise BizError("404", "会员不存在")
    m.score = (m.score or 0) + req.score
    db.commit()
    return ok({"memberId": m.id, "score": m.score})


@router.post("/levelList")
def level_list(ctx: Ctx = Depends(get_ctx)):
    return ok([{"levelId": 1, "name": "普通会员", "discount": 1.0},
               {"levelId": 2, "name": "银卡", "discount": 0.95},
               {"levelId": 3, "name": "金卡", "discount": 0.9}])


@router.post("/rechargeRule")
def recharge_rule(ctx: Ctx = Depends(get_ctx)):
    return ok([{"amount": 100, "gift": 0}, {"amount": 300, "gift": 30},
               {"amount": 500, "gift": 80}, {"amount": 1000, "gift": 200}])
