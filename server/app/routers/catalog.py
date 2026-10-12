"""catalog —— 商品域（含 CSV 同步通道）。"""
import os
from typing import List, Optional

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..config import OSS_BASE_URL
from ..db import get_db
from ..models import Product, ProductCategory, ProductUnit
from ..util import Ctx, get_ctx, ok, oss_dir_for, oss_url, write_csv

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
    out = []
    for key, (table, _fname, _cols) in SYNC_TABLES.items():
        out.append({"table": key, "version": _max_version(db, ctx.tenant_id, key)})
    return ok({"tables": out})


def _max_version(db: Session, tenant_id: int, key: str) -> int:
    model = {"Product": Product, "ProductCategory": ProductCategory, "ProductUnit": ProductUnit}[key]
    row = db.query(model).filter(model.tenant_id == tenant_id).order_by(model.version.desc()).first()
    return int(row.version) if row else 0


@router.post("/syncUrl")
def sync_url(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    """把各表导出成 CSV 放到对象存储目录，返回下载地址。"""
    base = oss_dir_for(ctx.tenant_id, ctx.store_id)
    urls = []
    for key, (_table, fname, cols) in SYNC_TABLES.items():
        ver = _max_version(db, ctx.tenant_id, key)
        path = os.path.join(base, "%s_%d.csv" % (fname, ver))
        write_csv(path, cols, _rows_for(db, ctx.tenant_id, key))
        urls.append({"table": key, "version": ver, "url": oss_url(path)})
    return ok({"urls": urls, "ossBase": OSS_BASE_URL})


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
        p.version = (p.version or 1) + 1
    else:
        p = Product(tenant_id=ctx.tenant_id)
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
    n = (db.query(Product)
           .filter(Product.tenant_id == ctx.tenant_id, Product.id.in_(req.ids))
           .update({Product.status: req.status}, synchronize_session=False))
    db.commit()
    return ok({"updated": n})


class BatchPriceReq(BaseModel):
    ids: List[int]
    price: float


@router.post("/batchUpdatePrice")
def batch_update_price(req: BatchPriceReq, ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    n = (db.query(Product)
           .filter(Product.tenant_id == ctx.tenant_id, Product.id.in_(req.ids))
           .update({Product.price: req.price}, synchronize_session=False))
    db.commit()
    return ok({"updated": n})


@router.post("/categoryList")
def category_list(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    rows = (db.query(ProductCategory)
              .filter(ProductCategory.tenant_id == ctx.tenant_id,
                      ProductCategory.data_status == 1)
              .order_by(ProductCategory.sort.asc()).all())
    return ok([{"id": c.id, "name": c.name, "pcType": c.pc_type, "sort": c.sort} for c in rows])
