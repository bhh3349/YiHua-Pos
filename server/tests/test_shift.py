"""开班 / 交班 / 交接记录（P0-5）。"""
import pytest

from conftest import envelope, new_client_no, post


def _products(price=50.0, qty=1):
    return [{"productId": 1004, "productName": "水煮牛肉", "prodPrice": price,
             "prodUnitAmt": price, "quantity": qty, "prodSalesAmt": price * qty}]


def _close_if_open(client):
    """测试之间互相独立：先确保没有进行中的班次。"""
    cur = envelope(post(client, "/pos/shift/open", {}))
    if cur["status"] == 0:
        post(client, "/pos/shift/close", {"shiftId": cur["shiftId"], "cashActual": 0})


@pytest.fixture()
def open_shift(client):
    _close_if_open(client)
    s = envelope(post(client, "/pos/shift/open", {"cashOpen": 300.0}))
    assert s["status"] == 0 and s["cashOpen"] == 300.0
    yield s
    _close_if_open(client)


def test_shift_open_is_idempotent(client):
    _close_if_open(client)
    a = envelope(post(client, "/pos/shift/open", {"cashOpen": 100.0}))
    b = envelope(post(client, "/pos/shift/open", {"cashOpen": 999.0}))
    assert a["shiftId"] == b["shiftId"], "已有进行中的班次时应直接返回"
    assert b["cashOpen"] == 100.0
    _close_if_open(client)


def test_shift_summary_shape(client, open_shift):
    for key in ("shiftId", "shiftNo", "terminalNo", "orderCount", "settledCount",
                "amountTotal", "cashAmount", "memberAmount", "refundAmount",
                "cashOpen", "cashShould", "cashActual", "status", "startAt"):
        assert key in open_shift, key
    assert open_shift["shiftNo"].startswith("S")


def test_shift_close_computes_cash_should(client, open_shift):
    """应有现金 = 备用金 + 现金收款 - 退款。"""
    cno = new_client_no("SHIFT")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 100.0}]})

    closed = envelope(post(client, "/pos/shift/close",
                           {"shiftId": open_shift["shiftId"], "cashActual": 400.0}))
    assert closed["status"] == 1
    assert closed["settledCount"] == 1
    assert closed["cashAmount"] == 100.0
    assert closed["amountTotal"] == 100.0
    assert closed["cashShould"] == 400.0        # 300 备用金 + 100 现金
    assert closed["cashActual"] == 400.0


def test_shift_close_refund_reduces_cash(client, open_shift):
    cno = new_client_no("SHIFT-REF")
    o = envelope(post(client, "/pos/order/submit",
                      {"clientOrderNo": cno, "products": _products(50.0, 2)}))
    post(client, "/pos/order/settle",
         {"orderNo": o["orderNo"], "payFlows": [{"pmSign": "2", "paymentAmt": 100.0}]})
    post(client, "/pos/order/refund", {"orderNo": o["orderNo"], "refundAmt": 40.0})

    closed = envelope(post(client, "/pos/shift/close",
                           {"shiftId": open_shift["shiftId"]}))
    assert closed["refundAmount"] == 40.0
    assert closed["cashShould"] == 360.0


def test_shift_close_twice_returns_409(client, open_shift):
    closed = envelope(post(client, "/pos/shift/close", {"shiftId": open_shift["shiftId"]}))
    dup = post(client, "/pos/shift/close", {"shiftId": closed["shiftId"]}).json()
    assert dup["code"] == 409
    assert __import__("json").loads(dup["data"])["shiftId"] == closed["shiftId"]


def test_shift_close_without_shift_id_after_close_returns_409(client, open_shift):
    envelope(post(client, "/pos/shift/close", {"shiftId": open_shift["shiftId"]}))
    dup = post(client, "/pos/shift/close", {}).json()
    assert dup["code"] == 409, "已交过班的门店重复交班应回 409 而不是 404"


def test_shift_close_other_store_shift_id_is_ignored(client, open_shift):
    """别的门店的 shiftId 不能拿来结本店的班。"""
    other = post(client, "/pos/shift/close",
                 {"shiftId": 999999, "cashActual": 1.0},
                 {"X-Store-Id": "200001"})
    # 999999 不存在 → 落到本店进行中的班次
    assert other.json()["code"] == 200
    assert envelope(post(client, "/pos/shift/open", {}))["status"] == 0


def test_handover_list(client):
    d = envelope(post(client, "/pos/handover/getHandoverList", {"pageNum": 1, "pageSize": 10}))
    assert {"total", "pages", "lastPage", "pageNum", "pageSize", "records"} <= set(d)
    assert d["records"]
    rec = d["records"][0]
    assert {"handoverNo", "handoverBeginTime", "handoverTime", "terminalNo",
            "workNo", "workName"} <= set(rec)
    assert rec["handoverNo"].startswith("S")


def test_shiftticket_upload_has_no_data_field(client):
    """真实抓包：这个接口的响应只有 key_sign / success / trace_no。"""
    b = post(client, "/pos/upload/shiftticketV1",
             {"shiftNo": "S000001", "ticket": "..."}).json()
    assert set(b) == {"key_sign", "success", "trace_no"}
    assert b["success"] is True


def test_new_contract_shift_endpoints(client, auth_headers, open_shift):
    cur = post(client, "/api/v1/shift/current", {}, auth_headers).json()
    assert cur["code"] == "200"
    assert cur["data"]["shiftId"] == int(open_shift["shiftId"])
