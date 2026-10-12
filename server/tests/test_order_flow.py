"""兼容层订单全流程 —— 开台 → 点单 → 预结 → 结账 → 退单 → 交班。"""
import pytest

from conftest import envelope, new_client_no, post


def _free_desk(client, nth=0):
    desks = [d for d in envelope(post(client, "/pos/desk/list", {}))
             if d.get("deskStatus") == 1]
    if len(desks) <= nth:
        pytest.skip("空闲桌台不够")
    return int(desks[nth]["deskId"])


def _products(price=38.0, qty=1, pid=1001, name="宫保鸡丁"):
    return [{"productId": pid, "productName": name, "prodPrice": price,
             "prodUnitAmt": price, "quantity": qty, "prodSalesAmt": price * qty}]


# --------------------------------------------------------------- 门店基础

def test_store_info(client):
    d = envelope(post(client, "/pos/store/info", {}))
    assert d["storeId"] == "200001"
    assert d["storeName"] and d["storeCode"]
    assert d["storeMode"] in (1, 2, 3)


def test_area_and_desk_list(client):
    areas = envelope(post(client, "/pos/area/list", {}))["areaList"]
    assert areas and all({"areaName", "id", "sort", "deskCount"} <= set(a) for a in areas)
    desks = envelope(post(client, "/pos/desk/list", {}))
    assert desks and all(d["deskStatus"] in (1, 2) for d in desks)


def test_surcharge_empty(client):
    assert envelope(post(client, "/pos/surcharge/query", {})) == []


def test_incr_product_data(client):
    b = envelope(post(client, "/pos/api/incr/productData",
                      {"currVersion": "0", "downloadDataType": "CyProduct"}))
    item = b["incrDataResult"][0]
    assert item["resultCode"] == "03"
    assert item["incrFileName"] == "CyProduct_0_%s.csv" % item["latestVersion"]

    latest = envelope(post(client, "/pos/api/incr/productData",
                           {"currVersion": item["latestVersion"],
                            "downloadDataType": "CyProduct"}))["incrDataResult"][0]
    assert latest["resultCode"] == "01"
    assert latest["incrFileName"] == ""


def test_incr_unknown_type(client):
    b = envelope(post(client, "/pos/api/incr/productData",
                      {"currVersion": "0", "downloadDataType": "CyUnknown"}))
    assert b["incrDataResult"][0]["resultCode"] == "03"


# --------------------------------------------------------------- 开台 / 点单

def test_open_desk_returns_order_and_surcharges(client):
    desk_id = _free_desk(client)
    d = envelope(post(client, "/pos/desk/openDesk",
                      {"deskId": desk_id, "customerNum": 4}))
    assert set(d) == {"orderDesk", "surcharges"}
    od = d["orderDesk"]
    assert od["orderNo"] and od["deskId"] == str(desk_id)
    assert od["deskStatus"] == 2
    assert od["customerNum"] == 4
    post(client, "/pos/desk/clearDesk", {"deskId": desk_id})


def test_open_unknown_desk_404(client):
    assert post(client, "/pos/desk/openDesk", {"deskId": 987654321}).json()["code"] == 404


def test_submit_returns_full_order_detail(client):
    cno = new_client_no("FLOW")
    d = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "orderType": 2, "customerNum": 2,
                       "products": _products(38.0, 2) + _products(2.0, 3, 1006, "米饭")}))
    assert d["amt"] == 82.0
    assert d["orderStatus"] == 1
    assert len(d["products"]) == 2
    assert {p["productName"] for p in d["products"]} == {"宫保鸡丁", "米饭"}
    assert d["numberOfDiners"] == 2


def test_submit_with_discount(client):
    cno = new_client_no("FLOW-DISC")
    d = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2),
                       "discStoreAmt": 6.0, "discAmt": 4.0}))
    assert d["amt"] == 76.0
    assert d["discAmt"] == 10.0


def test_pre_settle_does_not_persist(client):
    cno = new_client_no("FLOW-PRE")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2)}))
    pre = envelope(post(client, "/pos/order/preSettle", {"orderNo": o["orderNo"]}))
    assert pre["receiveAmt"] == pre["userReceiveAmt"] == 76.0
    assert envelope(post(client, "/pos/order/detail",
                         {"orderNo": o["orderNo"]}))["orderStatus"] == 1


def test_settle_cash_and_desk_released(client):
    cno = new_client_no("FLOW-CASH")
    desk_id = _free_desk(client)
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "deskId": desk_id, "orderType": 1,
                       "products": _products(38.0, 2)}))
    r = envelope(post(client, "/pos/order/settle",
                      {"orderNo": o["orderNo"], "payFlows": [
                          {"pmSign": "2", "paymentAmt": 76.0}]}))
    detail = r["orderDetail"]
    assert detail["orderStatus"] == 2
    assert detail["paymentType"] == 2
    assert detail["paidAmt"] == 76.0
    assert detail["payStatus"] == 1
    desks = {int(x["deskId"]): x["deskStatus"]
             for x in envelope(post(client, "/pos/desk/list", {}))}
    assert desks[desk_id] == 1, "结账后桌台必须释放"


def test_settle_member_pay_deducts_balance(client):
    """会员支付要真的扣余额，否则对不上账。"""
    cno = new_client_no("FLOW-MEMBER")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2),
                       "memberNo": "13800000001"}))
    assert o["memberNo"] == "13800000001"
    r = envelope(post(client, "/pos/order/settle",
                      {"orderNo": o["orderNo"], "memberNo": "13800000001",
                       "payFlows": [{"pmSign": "3", "paymentAmt": 76.0}]}))
    assert r["orderDetail"]["paymentType"] == 3


def test_multi_payment_split(client):
    cno = new_client_no("FLOW-SPLIT")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2)}))
    r = envelope(post(client, "/pos/order/settle",
                      {"orderNo": o["orderNo"], "payFlows": [
                          {"pmSign": "1", "paymentAmt": 60.0},
                          {"pmSign": "2", "paymentAmt": 40.0}]}))
    assert r["orderDetail"]["paidAmt"] == 100.0
    assert len(r["orderDetail"]["payFlows"]) == 2


def test_unknown_pay_sign_falls_back_to_other(client):
    """未知 pmSign 不报错，按「其他」支付方式收下（与抓包行为一致）。

    真实客户端会先把支付方式同步下来，走到这里说明本地字典没对上；
    宁可记成 OTHER 也不能让顾客结不了账。
    """
    cno = new_client_no("FLOW-BADPAY")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2)}))
    d = envelope(post(client, "/pos/order/settle",
                      {"orderNo": o["orderNo"],
                       "payFlows": [{"pmSign": "99", "paymentAmt": 76.0}]}))
    assert d["orderDetail"]["orderStatus"] == 2
    assert d["orderDetail"]["paymentType"] == 0


def test_refund_restores_member_balance(client, db):
    from app.models import Member
    cno = new_client_no("FLOW-REFUND-M")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2),
                       "memberNo": "13800000001"}))
    bal0 = float(db.get(Member, 1).balance)
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "memberNo": "13800000001",
          "payFlows": [{"pmSign": "3", "paymentAmt": 76.0}]})
    db.expire_all()
    assert float(db.get(Member, 1).balance) == round(bal0 - 76.0, 2)
    envelope(post(client, "/pos/order/refund", {"orderNo": o["orderNo"]}))
    db.expire_all()
    assert float(db.get(Member, 1).balance) == bal0


def test_refund_amount_over_balance_rejected(client):
    cno = new_client_no("FLOW-OVER")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 76.0}]})
    b = post(client, "/pos/order/refund",
             {"orderNo": o["orderNo"], "refundAmt": 999.0}).json()
    assert b["code"] == 409


def test_partial_refund_keeps_settled(client):
    """部分退款：订单仍是「已结账」，流水不能整单翻成已退（否则交班少算现金）。"""
    cno = new_client_no("FLOW-PART")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 100.0}]})
    d = envelope(post(client, "/pos/order/refund",
                      {"orderNo": o["orderNo"], "refundAmt": 30.0}))
    assert d["orderStatus"] == 2, "部分退款不该把订单整单置为已退"
    assert d["paidAmt"] == 100.0
    # payStatusCode 1=已付 9=已退：部分退款不能把整条流水标成已退
    assert all(p["payStatusCode"] == 1 for p in d["payFlows"])


def test_partial_refund_then_full_refund(client):
    """先退一部分、再退剩下的：累计退满才置为已退。"""
    cno = new_client_no("FLOW-PART2")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 100.0}]})
    envelope(post(client, "/pos/order/refund",
                  {"orderNo": o["orderNo"], "refundAmt": 30.0}))
    d = envelope(post(client, "/pos/order/refund",
                      {"orderNo": o["orderNo"], "refundAmt": 70.0}))
    assert d["orderStatus"] == 3
    b = post(client, "/pos/order/refund",
             {"orderNo": o["orderNo"], "refundAmt": 1.0}).json()
    assert b["code"] == 409, "退满之后再退必须拒绝"


def test_partial_refund_of_member_order_restores_only_that_part(client, db):
    """会员单部分退款只把对应金额还回卡里，不能整单退回去。"""
    from app.models import Member
    cno = new_client_no("FLOW-PART-M")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2),
                       "memberNo": "13800000001"}))
    bal0 = float(db.get(Member, 1).balance)
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "memberNo": "13800000001",
          "payFlows": [{"pmSign": "3", "paymentAmt": 100.0}]})
    envelope(post(client, "/pos/order/refund",
                  {"orderNo": o["orderNo"], "refundAmt": 40.0}))
    db.expire_all()
    assert float(db.get(Member, 1).balance) == round(bal0 - 60.0, 2)


def test_reverse_settle(client):
    """反结账：已结账订单回到未结账。"""
    cno = new_client_no("FLOW-REV")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 76.0}]})
    d = envelope(post(client, "/pos/order/reverseSettle", {"orderNo": o["orderNo"]}))
    assert d["orderStatus"] == 1


def test_unknown_order_detail_404(client):
    assert post(client, "/pos/order/detail", {"orderNo": "NOPE"}).json()["code"] == 404


def test_order_page_structure(client):
    d = envelope(post(client, "/pos/order/page", {"pageNum": 1, "pageSize": 5}))
    assert {"records", "total", "pageNum", "pageSize", "pages", "lastPage"} <= set(d)
    assert d["pageSize"] == 5
    assert d["records"] and "products" in d["records"][0]


def test_order_page_status_filter(client):
    d = envelope(post(client, "/pos/order/page",
                      {"pageNum": 1, "pageSize": 50, "orderStatus": 2}))
    assert all(r["orderStatus"] == 2 for r in d["records"])


def test_query_order_alias(client):
    """/pos/order/queryOrder 是 /pos/order/page 的别名，回的是分页结构。"""
    cno = new_client_no("FLOW-ALIAS")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(38.0, 2)}))
    d = envelope(post(client, "/pos/order/queryOrder",
                      {"orderNo": o["orderNo"], "pageNum": 1, "pageSize": 20}))
    assert {"records", "total", "pageNum", "pageSize"} <= set(d)
    assert [r["orderNo"] for r in d["records"]] == [o["orderNo"]]
