"""catalog —— 商品域（含 CSV 同步通道）。"""
import os
from datetime import datetime
from typing import Dict, List, Optional

from fastapi import APIRouter, Body, Depends, Request
from pydantic import BaseModel
from sqlalchemy import func
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Product, ProductCategory, ProductUnit
from ..util import (Ctx, get_ctx, ok, oss_dir_for, oss_url, resolve_oss_base,
                    write_csv)

router = APIRouter(prefix="/api/v1/catalog", tags=["catalog"])

# 可同步的实体 -> (表名, CSV 文件名, 列定义)
SYNC_TABLES = {
    "Product": ("product", "Product", ["id", "tenantId", "name", "code", "barcode",
                                       "categoryId", "unitId", "unitName", "price",
                                       "memberPrice", "cost", "specGroup", "productType",
                                       "weightWay", "status", "sort", "prePinYin", "version"]),
    "ProductCategory": ("product_category", "ProductCategory",
                        ["id", "tenantId", "name", "pcType", "sort", "dataStatus", "version"]),
    "ProductUnit": ("product_unit", "ProductUnit", ["id", "tenantId", "name", "sort", "version"]),
}


def _rows_for(db: Session, tenant_id: int, key: str):
    if key == "Product":
        return [[p.id, p.tenant_id, p.name, p.code, p.barcode, p.category_id, p.unit_id,
                 p.unit_name, float(p.price or 0), float(p.member_price or 0), float(p.cost or 0),
                 p.spec_group, p.product_type, p.weight_way, p.status, p.sort, p.pre_pin_yin,
                 p.version]
                for p in db.query(Product).filter(Product.tenant_id == tenant_id).all()]
    if key == "ProductCategory":
        return [[c.id, c.tenant_id, c.name, c.pc_type, c.sort, c.data_status, c.version]
                for c in db.query(ProductCategory).filter(ProductCategory.tenant_id == tenant_id).all()]
    if key == "ProductUnit":
        return [[u.id, u.tenant_id, u.name, u.sort, u.version]
                for u in db.query(ProductUnit).filter(ProductUnit.tenant_id == tenant_id).all()]
    return []


@router.post("/version")
def version(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    versions = {key: _max_version(db, ctx.tenant_id, key) for key in SYNC_TABLES}
    return ok({"versions": versions, "tables": [
        {"table": key, "version": value} for key, value in versions.items()],
        "serverTime": datetime.now().strftime("%Y-%m-%d %H:%M:%S")})


def _max_version(db: Session, tenant_id: int, key: str) -> int:
    model = {"Product": Product, "ProductCategory": ProductCategory, "ProductUnit": ProductUnit}[key]
    value = db.query(func.max(model.version)).filter(model.tenant_id == tenant_id).scalar()
    return int(value or 0)


def _next_product_version(db: Session, tenant_id: int) -> int:
    return _max_version(db, tenant_id, "Product") + 1


class SyncUrlReq(BaseModel):
    knownVersions: Optional[Dict[str, int]] = None


@router.post("/syncUrl")
def sync_url(request: Request, req: Optional[SyncUrlReq] = Body(None),
             ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    """按客户端已知版本生成需要下载的 CSV 地址；不传版本时保持全量行为。"""
    known_versions = req.knownVersions if req is not None else None
    base_dir = oss_dir_for(ctx.tenant_id, ctx.store_id)
    oss_base = resolve_oss_base(request)
    urls, unchanged = [], []
    for key, (_table, fname, cols) in SYNC_TABLES.items():
        ver = _max_version(db, ctx.tenant_id, key)
        if known_versions is not None and known_versions.get(key) == ver:
            unchanged.append(key)
            continue
        path = os.path.join(base_dir, "%s_%d.csv" % (fname, ver))
        write_csv(path, cols, _rows_for(db, ctx.tenant_id, key))
        urls.append({"table": key, "version": ver, "url": oss_url(path, oss_base)})
    return ok({"urls": urls, "unchanged": unchanged, "ossBase": oss_base})


class PageReq(BaseModel):
    keyword: str = ""
    categoryId: Optional[int] = None
    pageNumber: int = 1
    pageSize: int = 50
    onlyOnSale: bool = True


@router.post("/page")
def page(req: PageReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    q = db.query(Product).filter(Product.tenant_id == ctx.tenant_id)
    if req.onlyOnSale:
        q = q.filter(Product.status == 1)
    if req.categoryId:
        q = q.filter(Product.category_id == req.categoryId)
    if req.keyword:
        like = "%%%s%%" % req.keyword
        q = q.filter((Product.name.like(like)) | (Product.barcode.like(like))
                     | (Product.code.like(like)) | (Product.pre_pin_yin.like(like)))
    total = q.count()
    rows = (q.order_by(Product.sort.asc(), Product.id.asc())
             .offset((req.pageNumber - 1) * req.pageSize).limit(req.pageSize).all())
    return ok({"total": total, "pageNumber": req.pageNumber, "pageSize": req.pageSize,
               "list": [_to_dto(p) for p in rows]})


def _to_dto(p: Product) -> dict:
    return {"id": p.id, "name": p.name, "code": p.code, "barcode": p.barcode,
            "categoryId": p.category_id, "unitId": p.unit_id, "unitName": p.unit_name,
            "price": float(p.price or 0), "memberPrice": float(p.member_price or 0),
            "specGroup": p.spec_group, "productType": p.product_type,
            "weightWay": p.weight_way, "status": p.status, "sort": p.sort,
            "prePinYin": p.pre_pin_yin, "version": p.version}


class IdReq(BaseModel):
    id: int


@router.post("/detail")
def detail(req: IdReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    p = db.get(Product, req.id)
    if not p or p.tenant_id != ctx.tenant_id:
        return ok(None)
    return ok(_to_dto(p))


class SaveReq(BaseModel):
    id: Optional[int] = None
    name: str
    code: str = ""
    barcode: str = ""
    categoryId: Optional[int] = None
    unitId: Optional[int] = None
    unitName: str = ""
    price: float = 0
    memberPrice: float = 0
    cost: float = 0
    specGroup: str = ""
    productType: int = 1
    weightWay: int = 0
    status: int = 1
    sort: int = 0


@router.post("/save")
def save(req: SaveReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    if req.id:
        p = db.get(Product, req.id)
        if not p or p.tenant_id != ctx.tenant_id:
            return ok(None)
        p.version = _next_product_version(db, ctx.tenant_id)
    else:
        p = Product(tenant_id=ctx.tenant_id, version=_next_product_version(db, ctx.tenant_id))
        db.add(p)
    p.name, p.code, p.barcode = req.name, req.code, req.barcode
    p.unit_name, p.spec_group = req.unitName, req.specGroup
    p.category_id, p.unit_id = req.categoryId, req.unitId
    p.price, p.member_price, p.cost = req.price, req.memberPrice, req.cost
    p.product_type, p.weight_way = req.productType, req.weightWay
    p.status, p.sort = req.status, req.sort
    db.commit()
    return ok({"id": p.id})


class BatchStatusReq(BaseModel):
    ids: List[int]
    status: int


@router.post("/batchUpdateStatus")
def batch_update_status(req: BatchStatusReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(Product)
              .filter(Product.tenant_id == ctx.tenant_id, Product.id.in_(req.ids)).all())
    version = _next_product_version(db, ctx.tenant_id)
    for p in rows:
        p.status = req.status
        p.version = version
    n = len(rows)
    db.commit()
    return ok({"updated": n})


class BatchPriceReq(BaseModel):
    ids: List[int]
    price: float


@router.post("/batchUpdatePrice")
def batch_update_price(req: BatchPriceReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(Product)
              .filter(Product.tenant_id == ctx.tenant_id, Product.id.in_(req.ids)).all())
    version = _next_product_version(db, ctx.tenant_id)
    for p in rows:
        p.price = req.price
        p.version = version
    n = len(rows)
    db.commit()
    return ok({"updated": n})


@router.post("/categoryList")
def category_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(ProductCategory)
              .filter(ProductCategory.tenant_id == ctx.tenant_id,
                      ProductCategory.data_status == 1)
              .order_by(ProductCategory.sort.asc()).all())
    return ok([{"id": c.id, "name": c.name, "pcType": c.pc_type, "sort": c.sort} for c in rows])
