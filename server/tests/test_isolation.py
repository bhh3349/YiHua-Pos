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


def test_cross_store_client_order_no_allows_separate_orders(client, db, iso_store):
    """跨门店可以用同一个 clientOrderNo 各建一单。

    约束是复合唯一 (store_id, client_order_no)：
    幂等键的作用域是「单个门店」，不同门店之间互不干扰。
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

    # 主门店用同一个号建单：应当成功，且是另一张单
    second = post(client, "/api/v1/order/create", body,
                  {"X-Store-Id": str(MAIN_STORE),
                   "Authorization": "Bearer " + tok}).json()
    assert second["code"] == "200", second
    assert second["data"]["orderId"] != first["data"]["orderId"]

    db.expire_all()
    rows = db.query(Order).filter(Order.client_order_no == cno).all()
    assert len(rows) == 2, "两个门店应各自落一单"
    assert {r.store_id for r in rows} == {ISO_STORE, MAIN_STORE}


def test_same_store_client_order_no_still_idempotent(client, db, iso_store):
    """同一个门店重复提交同一个 clientOrderNo，仍然必须幂等（回 409 + 原单据）。"""
    cno = new_client_no("ISO-IDEM")
    body = {"clientOrderNo": cno, "orderType": 1,
            "items": [{"productId": 1003, "productName": "麻婆豆腐",
                       "price": 26.0, "qty": 1}]}
    tok = post(client, "/api/v1/auth/login",
               {"account": "admin", "password": "123456"}).json()["data"]["token"]
    h = dict(ISO_HEADERS, Authorization="Bearer " + tok)

    first = post(client, "/api/v1/order/create", body, h).json()
    assert first["code"] == "200", first

    again = post(client, "/api/v1/order/create", body, h).json()
    assert again["code"] == "409", again
    assert again["data"]["orderId"] == first["data"]["orderId"]

    db.expire_all()
    rows = db.query(Order).filter(Order.client_order_no == cno).all()
    assert len(rows) == 1



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
