"""多租户 / 多门店隔离 —— 所有查询必须带 tenant_id + store_id 过滤。

resolve_scope 会校验 store 是否真实存在，不存在就静默兜底到第一个门店，
所以这里必须往库里插一条真实的第二门店，才能测出隔离是否生效。
"""
import pytest

from conftest import SessionLocal, envelope, new_client_no, post
from app.models import Desk, DeskArea, Order, Store, Tenant

ISO_TENANT = 990002
ISO_STORE = 990002
MAIN_TENANT = 100001
MAIN_STORE = 200001

ISO_HEADERS = {"X-Tenant-Id": str(ISO_TENANT), "X-Store-Id": str(ISO_STORE)}


@pytest.fixture(scope="module")
def iso_store():
    """真实插入第二租户/门店/区域/桌台，测试结束清理。

    自己开 Session（不能用函数级的 db 夹具），否则 module 级夹具依赖
    function 级夹具会抛 ScopeMismatch。
    """
    session = SessionLocal()
    try:
        session.add(Tenant(id=ISO_TENANT, code="T990002", name="隔离测试租户",
                           edition="STD", status=1))
        session.add(Store(id=ISO_STORE, tenant_id=ISO_TENANT, code="S990002",
                          name="隔离测试门店", store_mode=1, status=1))
        session.add(DeskArea(id=990002, store_id=ISO_STORE, name="隔离区", sort=0))
        session.add(Desk(id=990002, store_id=ISO_STORE, area_id=990002,
                         name="隔离台", capacity=4, status=0))
        session.commit()
    finally:
        session.close()

    yield ISO_STORE

    session = SessionLocal()
    try:
        session.query(Order).filter(Order.store_id == ISO_STORE).delete()
        session.query(Desk).filter(Desk.store_id == ISO_STORE).delete()
        session.query(DeskArea).filter(DeskArea.store_id == ISO_STORE).delete()
        session.query(Store).filter(Store.id == ISO_STORE).delete()
        session.query(Tenant).filter(Tenant.id == ISO_TENANT).delete()
        session.commit()
    finally:
        session.close()


def test_desk_list_scoped_by_store(client, iso_store):
    mine = envelope(post(client, "/pos/desk/list", {}, ISO_HEADERS))
    assert [d["deskName"] for d in mine] == ["隔离台"]
    assert all(d["storeId"] == str(ISO_STORE) for d in mine)


def test_area_list_scoped_by_store(client, iso_store):
    areas = envelope(post(client, "/pos/area/list", {}, ISO_HEADERS))["areaList"]
    assert [a["areaName"] for a in areas] == ["隔离区"]
    assert areas[0]["deskCount"] == 1


def test_main_store_cannot_see_isolated_desks(client, iso_store):
    desks = envelope(post(client, "/pos/desk/list", {},
                          {"X-Store-Id": str(MAIN_STORE)}))
    assert "隔离台" not in [d["deskName"] for d in desks]


def test_operate_dict_scoped_by_store(client, iso_store):
    iso = envelope(post(client, "/pos/operate/getOperatelist", {}, ISO_HEADERS))
    main = envelope(post(client, "/pos/operate/getOperatelist", {},
                         {"X-Store-Id": str(MAIN_STORE)}))
    assert all(int(x["id"]) // 1000 == ISO_STORE for x in iso), iso[:3]
    assert {x["id"] for x in iso}.isdisjoint({x["id"] for x in main})


def test_order_created_in_isolated_store_not_visible_in_main(client, db, iso_store):
    cno = new_client_no("ISO")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": [
                          {"productId": 1003, "productName": "麻婆豆腐",
                           "prodPrice": 26.0, "prodUnitAmt": 26.0,
                           "quantity": 1, "prodSalesAmt": 26.0}]},
                      ISO_HEADERS))
    row = db.query(Order).filter(Order.order_no == o["orderNo"]).one()
    assert row.store_id == ISO_STORE and row.tenant_id == ISO_TENANT

    # 主门店查不到这单
    b = post(client, "/pos/order/detail", {"orderNo": o["orderNo"]},
             {"X-Store-Id": str(MAIN_STORE)}).json()
    assert b["code"] == 404

    # 隔离门店自己查得到
    assert envelope(post(client, "/pos/order/detail", {"orderNo": o["orderNo"]},
                         ISO_HEADERS))["orderNo"] == o["orderNo"]


def test_order_page_scoped(client, iso_store):
    iso = envelope(post(client, "/pos/order/page", {"pageNum": 1, "pageSize": 50},
                        ISO_HEADERS))
    main = envelope(post(client, "/pos/order/page", {"pageNum": 1, "pageSize": 50},
                         {"X-Store-Id": str(MAIN_STORE)}))
    iso_nos = {r["orderNo"] for r in iso["records"]}
    main_nos = {r["orderNo"] for r in main["records"]}
    assert iso_nos and main_nos
    assert iso_nos.isdisjoint(main_nos)


def test_cross_store_client_order_no_collision_is_handled(client, db, iso_store):
    """跨门店撞 clientOrderNo：t_order 上是全局唯一索引，不能漏 500。

    models.py:166 的 uk_client_no 不带 store_id，所以两家门店用同一个
    clientOrderNo 时数据库一定会拦。幂等查询按门店隔离挡不住这种情况，
    /api/v1/order/create 里用 IntegrityError 兜底回 409 + 原单据。
    「改成 (store_id, client_order_no) 复合唯一」已作为 schema 建议上报 Lead。
    """
    cno = new_client_no("ISO-COLLIDE")
    body = {"clientOrderNo": cno, "orderType": 1,
            "items": [{"productId": 1003, "productName": "麻婆豆腐",
                       "price": 26.0, "qty": 1}]}
    tok = post(client, "/api/v1/auth/login",
               {"account": "admin", "password": "123456"}).json()["data"]["token"]
    h = dict(ISO_HEADERS, Authorization="Bearer " + tok)
    first = post(client, "/api/v1/order/create", body, h).json()
    assert first["code"] == "200", first

    # 主门店用同一个号建单：必须回标准 409，不能是 500，也不能多落一张单
    b = post(client, "/api/v1/order/create", body,
             {"X-Store-Id": str(MAIN_STORE), "Authorization": "Bearer " + tok}).json()
    assert b["code"] == "409", b
    assert b["data"] and b["data"]["orderId"] == first["data"]["orderId"]

    from app.models import Order
    db.expire_all()
    rows = db.query(Order).filter(Order.client_order_no == cno).all()
    assert len(rows) == 1 and rows[0].store_id == ISO_STORE



def test_isolated_store_gets_its_own_defaults(client, iso_store):
    """KV 默认数据按 tenant_id + store_id 隔离，不能串店。"""
    iso = envelope(post(client, "/pos/print/strategy/info", {}, ISO_HEADERS))
    main = envelope(post(client, "/pos/print/strategy/info", {},
                         {"X-Store-Id": str(MAIN_STORE)}))
    assert iso["strategyName"] == main["strategyName"]      # 默认值相同
    # 但 id 派生自 scope，必须不同
    iso_list = envelope(post(client, "/pos/print/strategy/list", {}, ISO_HEADERS))
    main_list = envelope(post(client, "/pos/print/strategy/list", {},
                              {"X-Store-Id": str(MAIN_STORE)}))
    assert iso_list[0]["strategyId"] != main_list[0]["strategyId"]


def test_payload_store_id_also_works(client, iso_store):
    """除了请求头，报文里的 storeId 也要能定位门店。"""
    desks = envelope(post(client, "/pos/desk/list", {"storeId": ISO_STORE}))
    assert [d["deskName"] for d in desks] == ["隔离台"]


def test_unknown_store_falls_back_to_default(client):
    """门店号不存在时不报错，兜底到默认门店（保证客户端不会白屏）。"""
    desks = envelope(post(client, "/pos/desk/list", {},
                          {"X-Store-Id": "888888888"}))
    assert len(desks) >= 1


def test_terminal_no_reverse_lookup(client):
    """只带 posNo 时按 terminal 表反查门店。"""
    info = envelope(post(client, "/pos/store/info", {"posNo": "T0001"}))
    assert int(info["storeId"]) == MAIN_STORE
