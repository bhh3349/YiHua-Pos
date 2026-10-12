"""desk —— 桌台域。"""
from typing import List, Optional

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Desk, DeskArea, Order
from ..util import BizError, Ctx, get_ctx, ok

router = APIRouter(prefix="/api/v1/desk", tags=["desk"])


@router.post("/areaList")
def area_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(DeskArea).filter(DeskArea.store_id == ctx.store_id)
              .order_by(DeskArea.sort.asc(), DeskArea.id.asc()).all())
    return ok([{"id": a.id, "name": a.name, "sort": a.sort} for a in rows])


@router.post("/list")
def desk_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    desks = (db.query(Desk).filter(Desk.store_id == ctx.store_id)
               .order_by(Desk.area_id.asc(), Desk.id.asc()).all())
    out = []
    for d in desks:
        item = {"id": d.id, "areaId": d.area_id, "name": d.name, "capacity": d.capacity,
                "status": d.status, "order": None}
        if d.current_order_id:
            o = db.get(Order, d.current_order_id)
            if o:
                item["order"] = {"orderId": o.id, "orderNo": o.order_no,
                                 "amountTotal": float(o.amount_total or 0),
                                 "guestCount": 0,
                                 "openTime": o.created_at.strftime("%Y-%m-%d %H:%M:%S") if o.created_at else ""}
        out.append(item)
    return ok(out)


class OpenReq(BaseModel):
    deskId: int
    guestCount: int = 1
    orderType: int = 1


@router.post("/open")
def open_desk(req: OpenReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    d = db.get(Desk, req.deskId)
    if not d or d.store_id != ctx.store_id:
        raise BizError("404", "桌台不存在")
    if d.status == 1:
        # 已被占用：回 409 + 当前占用信息，收银端据此提示「这台已经开过单」，
        # 而不是悄悄复用一张别人正在用的单。
        info = {"deskId": d.id, "deskName": d.name, "deskStatus": d.status,
                "orderId": d.current_order_id or 0, "orderNo": "",
                "amountTotal": 0.0, "openTime": ""}
        if d.current_order_id:
            o = db.get(Order, d.current_order_id)
            if o is not None:
                info.update({
                    "orderId": o.id, "orderNo": o.order_no,
                    "amountTotal": float(o.amount_total or 0),
                    "openTime": (o.created_at.strftime("%Y-%m-%d %H:%M:%S")
                                 if o.created_at else ""),
                })
        raise BizError("409", "桌台已被占用", info)
    d.status = 1
    d.current_order_id = 0     # 订单在 order/create 时回填
    db.commit()
    return ok({"deskId": d.id, "orderId": 0, "reused": False})


class TransferReq(BaseModel):
    deskId: int
    targetDeskId: int


@router.post("/transfer")
def transfer(req: TransferReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    src, dst = db.get(Desk, req.deskId), db.get(Desk, req.targetDeskId)
    if not src or not dst or src.store_id != ctx.store_id or dst.store_id != ctx.store_id:
        raise BizError("404", "桌台不存在")
    if dst.status == 1:
        raise BizError("409", "目标桌台已占用")
    dst.status, dst.current_order_id = 1, src.current_order_id
    src.status, src.current_order_id = 0, 0
    if dst.current_order_id:
        o = db.get(Order, dst.current_order_id)
        if o:
            o.desk_id = dst.id
    db.commit()
    return ok({"from": src.id, "to": dst.id})


class MergeReq(BaseModel):
    deskId: int
    fromDeskIds: List[int]


@router.post("/merge")
def merge(req: MergeReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    main = db.get(Desk, req.deskId)
    if not main or main.store_id != ctx.store_id:
        raise BizError("404", "桌台不存在")
    merged = []
    for did in req.fromDeskIds:
        d = db.get(Desk, did)
        if d and d.store_id == ctx.store_id and d.id != main.id:
            if d.current_order_id:
                o = db.get(Order, d.current_order_id)
                if o:
                    o.desk_id = main.id
            d.status, d.current_order_id = 0, 0
            merged.append(d.id)
    main.status = 1
    db.commit()
    return ok({"mainDeskId": main.id, "mergedDeskIds": merged})


class DeskIdReq(BaseModel):
    deskId: int


@router.post("/clear")
def clear(req: DeskIdReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    d = db.get(Desk, req.deskId)
    if not d or d.store_id != ctx.store_id:
        raise BizError("404", "桌台不存在")
    d.status, d.current_order_id = 0, 0
    db.commit()
    return ok({"deskId": d.id, "status": 0})


class SaveDeskReq(BaseModel):
    id: Optional[int] = None
    areaId: int
    name: str
    capacity: int = 4


@router.post("/save")
def save(req: SaveDeskReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    d = db.get(Desk, req.id) if req.id else None
    if d and d.store_id != ctx.store_id:
        raise BizError("403", "无权操作")
    if not d:
        d = Desk(store_id=ctx.store_id)
        db.add(d)
    d.area_id, d.name, d.capacity = req.areaId, req.name, req.capacity
    db.commit()
    return ok({"id": d.id})


@router.post("/delete")
def delete(req: DeskIdReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    d = db.get(Desk, req.deskId)
    if not d or d.store_id != ctx.store_id:
        raise BizError("404", "桌台不存在")
    if d.status == 1:
        raise BizError("409", "桌台使用中，不能删除")
    db.delete(d)
    db.commit()
    return ok(None)
