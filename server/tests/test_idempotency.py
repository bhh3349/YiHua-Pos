"""幂等性 —— 验收标准：同一 clientOrderNo 提交 10 次，数据库只有 1 条。"""
import uuid

import pytest

from conftest import envelope, new_client_no, post
from app.models import Order


def _products():
    return [{"productId": 1003, "productName": "麻婆豆腐", "prodPrice": 26.0,
             "prodUnitAmt": 26.0, "quantity": 2, "prodSalesAmt": 52.0}]


def test_submit_same_client_no_ten_times(client, db):
    cno = new_client_no("IDEM")
    body = {"clientOrderNo": cno, "orderType": 2, "products": _products()}

    codes, order_nos = [], []
    for _ in range(10):
        b = post(client, "/pos/order/submit", body).json()
        codes.append(b["code"])
        if b["data"]:
            order_nos.append(__import__("json").loads(b["data"]).get("orderNo"))

    assert codes == [200] + [409] * 9, codes
    assert len(set(order_nos)) == 1 and order_nos[0], order_nos

    rows = db.query(Order).filter(Order.client_order_no == cno).all()
    assert len(rows) == 1, "同一 clientOrderNo 落了多张单"


def test_duplicate_returns_full_original_document(client):
    """409 的 data 必须是原单据完整信息，客户端据此补本地缺的字段。"""
    cno = new_client_no("IDEM-DOC")
    body = {"clientOrderNo": cno, "orderType": 2, "customerNum": 3,
            "products": _products()}
    first = envelope(post(client, "/pos/order/submit", body))
    dup = post(client, "/pos/order/submit", body).json()

    assert dup["code"] == 409 and dup["success"] is False
    doc = __import__("json").loads(dup["data"])
    assert doc["orderNo"] == first["orderNo"]
    assert doc["amt"] == first["amt"] == 52.0
    assert doc["numberOfDiners"] == 3
    assert [p["productName"] for p in doc["products"]] == ["麻婆豆腐"]
    assert doc["products"][0]["quantity"] == 2


def test_client_order_no_is_unique_in_db(client, db):
    cno = new_client_no("IDEM-UK")
    post(client, "/pos/order/submit", {"clientOrderNo": cno, "products": _products()})
    post(client, "/pos/order/submit", {"clientOrderNo": cno, "products": _products()})

    dup = (db.query(Order.client_order_no)
             .filter(Order.client_order_no == cno).count())
    assert dup == 1


def test_missing_client_order_no_falls_back_to_order_no(client):
    """客户端改单时不带 clientOrderNo，用 orderNo 兜底，同样不许重复落单。"""
    cno = new_client_no("IDEM-ORD")
    first = envelope(post(client, "/pos/order/submit",
                          {"clientOrderNo": cno, "products": _products()}))
    again = envelope(post(client, "/pos/order/submit",
                          {"orderNo": first["orderNo"], "products": _products()}))
    assert again["orderNo"] == first["orderNo"]


def test_submit_without_any_no_is_rejected(client):
    b = post(client, "/pos/order/submit", {"products": _products()}).json()
    assert b["code"] == 500
    assert b["success"] is False


def test_settle_idempotent_route(client):
    """settleIdempotent 与 settle 同语义：重复结账回 409 + 原单据。"""
    cno = new_client_no("IDEM-SETTLE")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products()}))
    body = {"orderNo": o["orderNo"], "clientOrderNo": cno,
            "payFlows": [{"pmSign": "2", "paymentAmt": 52.0}]}
    first = envelope(post(client, "/pos/order/settleIdempotent", body))
    assert first["orderDetail"]["orderStatus"] == 2
    dup = post(client, "/pos/order/settleIdempotent", body).json()
    assert dup["code"] == 409
    assert __import__("json").loads(dup["data"])["orderDetail"]["orderStatus"] == 2


def test_open_desk_is_idempotent(client, db):
    """已开台再开台 → 回原单，不会开出两张单。"""
    desk_id = _free_desk(client)
    a = envelope(post(client, "/pos/desk/openDesk", {"deskId": desk_id, "customerNum": 2}))
    b = envelope(post(client, "/pos/desk/openDesk", {"deskId": desk_id, "customerNum": 2}))
    assert a["orderDesk"]["orderNo"] == b["orderDesk"]["orderNo"]
    post(client, "/pos/desk/clearDesk", {"deskId": desk_id})


def test_cancel_twice_returns_409(client):
    desk_id = _free_desk(client)
    o = envelope(post(client, "/pos/desk/openDesk", {"deskId": desk_id}))
    no = o["orderDesk"]["orderNo"]
    assert envelope(post(client, "/pos/order/cancel", {"orderNo": no}))["orderStatus"] == 4
    dup = post(client, "/pos/order/cancel", {"orderNo": no}).json()
    assert dup["code"] == 409


def test_refund_twice_returns_409(client):
    cno = new_client_no("IDEM-REFUND")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products()}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 52.0}]})
    assert envelope(post(client, "/pos/order/refund", {"orderNo": o["orderNo"]}))["orderStatus"] == 3
    dup = post(client, "/pos/order/refund", {"orderNo": o["orderNo"]}).json()
    assert dup["code"] == 409


def test_settled_order_cannot_be_modified(client):
    cno = new_client_no("IDEM-LOCK")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products()}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 52.0}]})
    b = post(client, "/pos/order/submit",
             {"orderNo": o["orderNo"], "products": _products()}).json()
    assert b["code"] == 409


def test_payment_shortfall_rejected(client):
    cno = new_client_no("IDEM-SHORT")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products()}))
    b = post(client, "/pos/order/settle",
             {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 10.0}]}).json()
    assert b["code"] == 409


def test_offline_settle_creates_missing_order(client, db):
    """离线补传：服务端没这单，但客户端已经结了 → 先落单再结，仍保证幂等。"""
    cno = new_client_no("IDEM-OFFLINE")
    body = {"clientOrderNo": cno, "orderType": 1, "products": _products(),
            "payFlows": [{"pmSign": "2", "paymentAmt": 52.0}]}
    r = envelope(post(client, "/pos/order/settle", body))
    assert r["orderDetail"]["orderStatus"] == 2
    assert db.query(Order).filter(Order.client_order_no == cno).count() == 1


# --------------------------------------------------------------- 工具

def _free_desk(client):
    desks = envelope(post(client, "/pos/desk/list", {}))
    for d in desks:
        if d.get("deskStatus") == 1:
            return int(d["deskId"])
    pytest.fail("没有空闲桌台")
