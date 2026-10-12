"""演示数据 —— 服务端首次启动时自动初始化，保证开箱即用。"""
from datetime import datetime

from .db import SessionLocal
from .models import (Desk, DeskArea, Member, Product, ProductCategory, ProductUnit,
                     Store, SysRole, SysUser, Tenant, Terminal)
from .security import hash_password

DEMO_TENANT = dict(id=100001, code="T100001", name="示例餐饮集团", edition="STD")
DEMO_STORE = dict(id=200001, tenant_id=100001, code="S200001", name="示例门店(总店)",
                  store_mode=1, address="示例市示例区示例路 1 号", phone="400-000-0000")
DEMO_ROLE = dict(id=1, tenant_id=100001, name="店长", permissions_json=["*"])
DEMO_USER = dict(id=1, tenant_id=100001, store_id=200001, account="admin",
                 password="123456", name="店长", role_id=1)

CATEGORIES = [(1, "热菜", 1, 1), (2, "凉菜", 1, 2), (3, "主食", 1, 3),
              (4, "饮品", 1, 4), (5, "套餐", 1, 5)]
UNITS = [(1, "份", 1), (2, "例", 2), (3, "斤", 3), (4, "瓶", 4)]
PRODUCTS = [
    (1001, "宫保鸡丁", "P1001", "6901001001", 1, 1, 38.00, 35.00, 1, "微辣/中辣/特辣", "GBJD"),
    (1002, "鱼香肉丝", "P1002", "6901001002", 1, 1, 32.00, 30.00, 1, "", "YXRS"),
    (1003, "麻婆豆腐", "P1003", "6901001003", 1, 1, 26.00, 24.00, 1, "", "MPDF"),
    (1004, "口水鸡", "P1004", "6901001004", 2, 2, 28.00, 26.00, 1, "", "KSJ"),
    (1005, "拍黄瓜", "P1005", "6901001005", 2, 1, 12.00, 10.00, 1, "", "PHG"),
    (1006, "米饭", "P1006", "6901001006", 3, 1, 2.00, 2.00, 1, "", "MF"),
    (1007, "手工水饺", "P1007", "6901001007", 3, 1, 18.00, 16.00, 1, "", "SGSJ"),
    (1008, "鲜榨橙汁", "P1008", "6901001008", 4, 4, 15.00, 13.00, 1, "中杯/大杯", "XZCZ"),
    (1009, "可口可乐", "P1009", "6901001009", 4, 4, 6.00, 6.00, 1, "", "KKKL"),
    (1010, "酸菜鱼", "P1010", "6901001010", 1, 2, 68.00, 62.00, 1, "微辣/中辣", "SCY"),
    (1011, "双人套餐A", "P1011", "6901001011", 5, 1, 128.00, 118.00, 2, "", "SRTCA"),
    (1012, "时令水果", "P1012", "6901012", 4, 3, 12.80, 12.00, 3, "", "SLGS"),
]
AREAS = [(1, "大厅", 1), (2, "包间", 2), (3, "二楼", 3)]
DESKS = [(1, 1, "A01", 4), (2, 1, "A02", 4), (3, 1, "A03", 6), (4, 1, "A05", 2),
         (5, 2, "B01", 10), (6, 2, "B02", 12), (7, 3, "C01", 4), (8, 3, "C02", 4)]
MEMBERS = [(1, "13800000001", "张三", 2, 500.00, 1200),
           (2, "13800000002", "李四", 1, 88.00, 300)]


def seed(db) -> dict:
    """幂等灌入演示数据，返回统计"""
    if not db.get(Tenant, DEMO_TENANT["id"]):
        db.add(Tenant(**DEMO_TENANT))
    if not db.get(Store, DEMO_STORE["id"]):
        db.add(Store(**DEMO_STORE))
    if not db.get(SysRole, DEMO_ROLE["id"]):
        db.add(SysRole(**DEMO_ROLE))
    if not db.get(SysUser, DEMO_USER["id"]):
        db.add(SysUser(id=DEMO_USER["id"], tenant_id=DEMO_USER["tenant_id"],
                       store_id=DEMO_USER["store_id"], account=DEMO_USER["account"],
                       pwd_hash=hash_password(DEMO_USER["password"]),
                       name=DEMO_USER["name"], role_id=DEMO_USER["role_id"], status=1))
    if not db.query(Terminal).filter(Terminal.store_id == DEMO_STORE["id"]).first():
        db.add(Terminal(store_id=DEMO_STORE["id"], no="T0001", name="前台收银机", status=1))
    db.commit()

    for cid, name, ptype, sort in CATEGORIES:
        if not db.get(ProductCategory, cid):
            db.add(ProductCategory(id=cid, tenant_id=DEMO_TENANT["id"], name=name,
                                   pc_type=ptype, sort=sort, data_status=1, version=1))
    for uid, name, sort in UNITS:
        if not db.get(ProductUnit, uid):
            db.add(ProductUnit(id=uid, tenant_id=DEMO_TENANT["id"], name=name, sort=sort, version=1))
    db.commit()

    unit_name = {u[0]: u[1] for u in UNITS}
    for pid, name, code, bar, cat, uid, price, mprice, ptype, spec, py in PRODUCTS:
        if not db.get(Product, pid):
            db.add(Product(id=pid, tenant_id=DEMO_TENANT["id"], name=name, code=code,
                           barcode=bar, category_id=cat, unit_id=uid,
                           unit_name=unit_name.get(uid, "份"), price=price, member_price=mprice,
                           cost=round(price * 0.4, 2), spec_group=spec, product_type=ptype,
                           weight_way=1 if ptype == 3 else 0, status=1, sort=pid,
                           pre_pin_yin=py, version=1))
    db.commit()

    for aid, name, sort in AREAS:
        if not db.get(DeskArea, aid):
            db.add(DeskArea(id=aid, store_id=DEMO_STORE["id"], name=name, sort=sort))
    for did, area, name, cap in DESKS:
        if not db.get(Desk, did):
            db.add(Desk(id=did, store_id=DEMO_STORE["id"], area_id=area, name=name,
                        capacity=cap, status=0, current_order_id=0))
    for mid, phone, name, level, bal, score in MEMBERS:
        if not db.get(Member, mid):
            db.add(Member(id=mid, tenant_id=DEMO_TENANT["id"], phone=phone, name=name,
                          level_id=level, balance=bal, score=score, status=1))
    db.commit()

    return {"tenant": DEMO_TENANT["name"], "store": DEMO_STORE["name"],
            "account": DEMO_USER["account"], "password": DEMO_USER["password"],
            "products": len(PRODUCTS), "desks": len(DESKS), "members": len(MEMBERS)}


def seed_if_empty() -> bool:
    """库为空时自动初始化，返回是否执行了初始化"""
    db = SessionLocal()
    try:
        if db.query(Tenant).count() > 0:
            return False
        info = seed(db)
        print("[初始化] 已写入演示数据: %s / %s  账号 %s/%s"
              % (info["tenant"], info["store"], info["account"], info["password"]))
        return True
    except Exception as e:
        print("[初始化] 失败: %r" % e)
        return False
    finally:
        db.close()
