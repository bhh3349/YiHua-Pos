"""
按参考系统客户端实体列名导出商品 CSV —— 让兼容模式下的客户端能拉到商品。

路径规则与 ProductDownloadService.GetDownUrl 完全一致：

    {OSS}/{md5_16(tenantId)}/cyproduct/{表}.csv                      ← 9 张租户级表
    {OSS}/{md5_16(tenantId)}/{md5_16(storeId)}/cyproduct/{表}.csv    ← 4 张门店级表

其中 md5_16 = MD5 十六进制串的**前 16 位**（客户端 Md5Util.Md5_16）。
"""
import os

from .config import OSS_DIR
from .db import SessionLocal
from .models import Product, ProductCategory, ProductUnit, Store, Tenant
from .util import md5_16, write_csv

LEGACY_COLUMNS = {
    "CyProduct": ["id", "tenantId", "createUser", "createDate", "modifyUser", "modifyDate",
                  "version", "productName", "coverPicUrl", "productType", "weightWay", "isCharge",
                  "unitId", "unitName", "memo", "specGroupName", "businessTime",
                  "salesRestriction", "floatingPrice", "addSalesNum", "onSalesNum", "packageFee",
                  "prePinYin", "description", "dataStatus", "tradeType", "discFlag"],
    "CyProductCategory": ["id", "tenantId", "createUser", "createDate", "modifyUser",
                          "modifyDate", "version", "storeId", "pcName", "pcType", "sort",
                          "pcMode", "picUrl", "dataStatus"],
    "CyProductUnit": ["id", "tenantId", "createUser", "createDate", "modifyUser", "modifyDate",
                      "version", "puName", "type", "builtIn", "sort", "memo", "dataStatus"],
    # 门店商品上架关系（不导这张表，客户端商品列表为空）
    "CyStoreProduct": ["id", "tenantId", "createUser", "createDate", "modifyUser", "modifyDate",
                       "version", "storeId", "productId", "specId", "salePrice", "vipPrice",
                       "dataStatus"],
    # 商品-分类展示关系
    "CyProductCategoryRelation": ["id", "tenantId", "createUser", "createDate", "modifyUser",
                                  "modifyDate", "version", "storeId", "productId", "sort",
                                  "categoryId", "categoryName", "pcType", "dataStatus"],
}

# 客户端会给这 4 张表拼上「门店目录」
STORE_SCOPED = ("CyProductCategory", "CyStoreProduct", "CyProductCategoryRelation", "CyProductChannel")
# 其余 9 张只在租户目录下
TENANT_SCOPED = ("CyProduct", "CyProductSpec", "CyProductSpecRelation", "CyProductUnit",
                 "CyProductAttrGroup", "CyProductAttr", "CyProductAttrRelation",
                 "CyProductContract", "CyProductGroupRelation")

PLACEHOLDER_HEADER = ["id", "tenantId", "createUser", "createDate", "modifyUser",
                      "modifyDate", "version"]
NOW = "2026-01-01 00:00:00"


def _row(cols, values):
    return [values.get(c, "") for c in cols]


def export_legacy_csv() -> dict:
    db = SessionLocal()
    try:
        tenant = db.query(Tenant).order_by(Tenant.id).first()
        store = db.query(Store).order_by(Store.id).first()
        if not tenant or not store:
            return {"ok": False, "reason": "无租户/门店"}

        t16, s16 = md5_16(tenant.id), md5_16(store.id)
        dir_tenant = os.path.join(OSS_DIR, t16, "cyproduct")
        dir_store = os.path.join(OSS_DIR, t16, s16, "cyproduct")
        os.makedirs(dir_tenant, exist_ok=True)
        os.makedirs(dir_store, exist_ok=True)

        # ---- 租户级：CyProduct ----
        # 注意两个客户端过滤条件（QueryProductService.BuildQueryProductListSql）：
        #   1) AND (p.salesRestriction = 0 OR p.salesRestriction IS NULL)  -> 必须为 0
        #   2) AND p.productType IN (0,2,3,4,5)                            -> 不能为 1
        PT_MAP = {1: 0, 2: 2, 3: 3}     # 自有库: 1普通 2套餐 3称重 -> 客户端编码
        cols = LEGACY_COLUMNS["CyProduct"]
        rows = [_row(cols, {
            "id": p.id, "tenantId": p.tenant_id, "createUser": "system", "createDate": NOW,
            "modifyUser": "system", "modifyDate": NOW, "version": p.version or 1,
            "productName": p.name, "productType": PT_MAP.get(p.product_type, 0),
            "weightWay": p.weight_way,
            "isCharge": 1, "unitId": p.unit_id, "unitName": p.unit_name,
            "specGroupName": p.spec_group or "", "businessTime": "00:00-23:59",
            "salesRestriction": 0, "floatingPrice": 0, "addSalesNum": "0.0001",
            "onSalesNum": "0.0001", "packageFee": 0, "prePinYin": p.pre_pin_yin or "",
            "dataStatus": 1 if p.status == 1 else 99, "tradeType": 1, "discFlag": 1,
        }) for p in db.query(Product).filter(Product.tenant_id == tenant.id).all()]
        write_csv(os.path.join(dir_tenant, "CyProduct.csv"), cols, rows)
        n_products = len(rows)

        # ---- 租户级：CyProductUnit ----
        cols = LEGACY_COLUMNS["CyProductUnit"]
        rows = [_row(cols, {
            "id": u.id, "tenantId": u.tenant_id, "createUser": "system", "createDate": NOW,
            "modifyUser": "system", "modifyDate": NOW, "version": u.version or 1,
            "puName": u.name, "type": 0, "builtIn": 1, "sort": u.sort, "memo": "", "dataStatus": 1,
        }) for u in db.query(ProductUnit).filter(ProductUnit.tenant_id == tenant.id).all()]
        write_csv(os.path.join(dir_tenant, "CyProductUnit.csv"), cols, rows)

        # ---- 门店级：CyProductCategory ----
        cols = LEGACY_COLUMNS["CyProductCategory"]
        rows = [_row(cols, {
            "id": c.id, "tenantId": c.tenant_id, "createUser": "system", "createDate": NOW,
            "modifyUser": "system", "modifyDate": NOW, "version": c.version or 1,
            "storeId": store.id, "pcName": c.name, "pcType": c.pc_type, "sort": c.sort,
            "pcMode": 0, "picUrl": "", "dataStatus": 1 if c.data_status == 1 else 99,
        }) for c in db.query(ProductCategory).filter(ProductCategory.tenant_id == tenant.id).all()]
        write_csv(os.path.join(dir_store, "CyProductCategory.csv"), cols, rows)

        # ---- 门店级：门店商品上架关系（商品列表靠这张表） ----
        products = db.query(Product).filter(Product.tenant_id == tenant.id).all()
        cats = {c.id: c for c in db.query(ProductCategory)
                  .filter(ProductCategory.tenant_id == tenant.id).all()}

        cols = LEGACY_COLUMNS["CyStoreProduct"]
        rows = [_row(cols, {
            "id": p.id, "tenantId": p.tenant_id, "createUser": "system", "createDate": NOW,
            "modifyUser": "system", "modifyDate": NOW, "version": p.version or 1,
            "storeId": store.id, "productId": p.id, "specId": 0,
            "salePrice": float(p.price or 0), "vipPrice": float(p.member_price or 0),
            "dataStatus": 1 if p.status == 1 else 99,
        }) for p in products]
        write_csv(os.path.join(dir_store, "CyStoreProduct.csv"), cols, rows)

        # ---- 门店级：商品-分类展示关系 ----
        cols = LEGACY_COLUMNS["CyProductCategoryRelation"]
        rows = []
        for p in products:
            c = cats.get(p.category_id)
            rows.append(_row(cols, {
                "id": p.id, "tenantId": p.tenant_id, "createUser": "system", "createDate": NOW,
                "modifyUser": "system", "modifyDate": NOW, "version": p.version or 1,
                "storeId": store.id, "productId": p.id, "sort": p.sort,
                "categoryId": p.category_id or 0,
                "categoryName": c.name if c else "",
                "pcType": c.pc_type if c else 1,
                "dataStatus": 1 if p.status == 1 else 99,
            }))
        write_csv(os.path.join(dir_store, "CyProductCategoryRelation.csv"), cols, rows)

        # ---- 其余表：表头占位，避免客户端导入报错 ----
        for t in TENANT_SCOPED:
            if t in ("CyProduct", "CyProductUnit"):
                continue
            write_csv(os.path.join(dir_tenant, t + ".csv"), PLACEHOLDER_HEADER, [])
        for t in STORE_SCOPED:
            if t in ("CyProductCategory", "CyStoreProduct", "CyProductCategoryRelation"):
                continue
            write_csv(os.path.join(dir_store, t + ".csv"), PLACEHOLDER_HEADER, [])

        return {"ok": True, "tenantDir": dir_tenant, "storeDir": dir_store,
                "tenant16": t16, "store16": s16, "products": n_products}
    finally:
        db.close()
