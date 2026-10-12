"""新契约 /api/v1/* —— 自有客户端使用的接口面（data 是真实对象，不是二次编码）。"""
import pytest

from conftest import new_client_no, post

V1 = "/api/v1"


def _v1(client, path, body, headers):
    r = post(client, V1 + path, body, headers)
    assert r.status_code == 200, r.text
    b = r.json()
    assert b["code"] == "200", (path, b)
    return b["data"]


# --------------------------------------------------------------- 鉴权

def test_health(client):
    b = client.get("/health").json()
    assert b["ok"] is True and b["app"]


def test_login_returns_token_and_context(client):
    b = post(client, V1 + "/auth/login",
             {"account": "admin", "password": "123456"}).json()
    assert b["code"] == "200"
    d = b["data"]
    assert d["token"] and d["refreshToken"]
    assert d["userInfo"]["account"] == "admin"
    assert d["tenantInfo"]["tenantId"] == 100001
    assert d["storeInfo"]["storeId"] == 200001
    assert d["systemData"] is None


def test_login_wrong_password(client):
    b = post(client, V1 + "/auth/login",
             {"account": "admin", "password": "bad"}).json()
    assert b["code"] == "401"


def test_login_unknown_account(client):
    b = post(client, V1 + "/auth/login",
             {"account": "nobody", "password": "x"}).json()
    assert b["code"] == "401"


def test_profile_requires_token(client):
    b = post(client, V1 + "/auth/profile", {}).json()
    assert b["code"] == "401"


def test_profile_with_token(client, auth_headers):
    d = _v1(client, "/auth/profile", {}, auth_headers)
    assert d["account"] == "admin"
    assert d["userId"] == 1 and d["roleName"]
    assert "*" in d["permissions"]


def test_refresh_token(client):
    tok = post(client, V1 + "/auth/login",
               {"account": "admin", "password": "123456"}).json()["data"]["refreshToken"]
    d = _v1(client, "/auth/refresh", {"refreshToken": tok}, {})
    assert d["token"]


def test_refresh_with_garbage(client):
    b = post(client, V1 + "/auth/refresh", {"refreshToken": "nope"}).json()
    assert b["code"] == "401"


def test_logout(client, auth_headers):
    assert _v1(client, "/auth/logout", {}, auth_headers) is None


def test_login_confirm(client):
    d = _v1(client, "/auth/loginConfirm", {"loginTicket": "", "userId": 1}, {})
    assert d["userInfo"]["account"] == "admin"


# --------------------------------------------------------------- 商品

def test_catalog_page(client, auth_headers):
    d = _v1(client, "/catalog/page", {"pageNumber": 1, "pageSize": 5}, auth_headers)
    assert d["total"] >= 12
    assert len(d["list"]) == 5
    p = d["list"][0]
    assert {"id", "name", "price", "unitName"} <= set(p)


def test_catalog_page_keyword(client, auth_headers):
    d = _v1(client, "/catalog/page", {"keyword": "宫保"}, auth_headers)
    assert [r["name"] for r in d["list"]] == ["宫保鸡丁"]


def test_catalog_category_list(client, auth_headers):
    rows = _v1(client, "/catalog/categoryList", {}, auth_headers)
    assert {r["name"] for r in rows} >= {"热菜", "凉菜", "主食"}


def test_catalog_detail(client, auth_headers):
    d = _v1(client, "/catalog/detail", {"id": 1001}, auth_headers)
    assert d["name"] == "宫保鸡丁" and d["code"] == "P1001"


def test_catalog_detail_missing(client, auth_headers):
    """查不到商品回 data=null，不报错（客户端按「没这条」处理）。"""
    assert _v1(client, "/catalog/detail", {"id": 999999}, auth_headers) is None


def test_catalog_version(client, auth_headers):
    d = _v1(client, "/catalog/version", {}, auth_headers)
    assert {t["table"] for t in d["tables"]} == {"Product", "ProductCategory", "ProductUnit"}
    assert all(t["version"] > 0 for t in d["tables"])


def test_catalog_sync_url(client, auth_headers):
    d = _v1(client, "/catalog/syncUrl", {}, auth_headers)
    assert d["urls"] and all(f["url"].startswith("http") for f in d["urls"])
    assert {f["table"] for f in d["urls"]} == {"Product", "ProductCategory", "ProductUnit"}


def test_catalog_incremental_sync_returns_only_changed_table(client, auth_headers):
    before = _v1(client, "/catalog/version", {}, auth_headers)
    versions = before["versions"]
    assert versions == {row["table"]: row["version"] for row in before["tables"]}
    assert before["serverTime"]

    unchanged = _v1(client, "/catalog/syncUrl", {"knownVersions": versions}, auth_headers)
    assert unchanged["urls"] == []
    assert set(unchanged["unchanged"]) == set(versions)

    _v1(client, "/catalog/batchUpdatePrice", {"ids": [1001], "price": 39.0}, auth_headers)
    after = _v1(client, "/catalog/version", {}, auth_headers)["versions"]
    assert after["Product"] > versions["Product"]
    assert after["ProductCategory"] == versions["ProductCategory"]
    assert after["ProductUnit"] == versions["ProductUnit"]

    changed = _v1(client, "/catalog/syncUrl", {"knownVersions": versions}, auth_headers)
    assert [row["table"] for row in changed["urls"]] == ["Product"]
    assert changed["urls"][0]["version"] == after["Product"]
    assert changed["urls"][0]["url"].endswith("Product_%d.csv" % after["Product"])
    _v1(client, "/catalog/batchUpdatePrice", {"ids": [1001], "price": 38.0}, auth_headers)


def test_catalog_save_and_batch(client, auth_headers):
    d = _v1(client, "/catalog/save",
            {"id": 1001, "name": "宫保鸡丁(大份)", "price": 42.0}, auth_headers)
    assert d["id"] == 1001
    back = _v1(client, "/catalog/detail", {"id": 1001}, auth_headers)
    assert back["name"] == "宫保鸡丁(大份)" and back["price"] == 42.0
    _v1(client, "/catalog/save", {"id": 1001, "name": "宫保鸡丁", "price": 38.0}, auth_headers)

    _v1(client, "/catalog/batchUpdateStatus", {"ids": [1001], "status": 1}, auth_headers)
    _v1(client, "/catalog/batchUpdatePrice", {"ids": [1001], "price": 38.0}, auth_headers)


# --------------------------------------------------------------- 桌台

def test_desk_area_list(client, auth_headers):
    rows = _v1(client, "/desk/areaList", {}, auth_headers)
    assert rows and {"id", "name", "sort"} <= set(rows[0])


def test_desk_list(client, auth_headers):
    rows = _v1(client, "/desk/list", {}, auth_headers)
    assert len(rows) >= 8
    assert {"id", "areaId", "name", "capacity", "status", "order"} <= set(rows[0])


def test_desk_open_transfer_clear(client, auth_headers):
    free = [d for d in _v1(client, "/desk/list", {}, auth_headers) if d["status"] == 0]
    if len(free) < 2:
        pytest.skip("空闲桌台不足")
    a, b = free[0]["id"], free[1]["id"]
    d = _v1(client, "/desk/open", {"deskId": a, "numberOfDiners": 2}, auth_headers)
    assert d["deskId"] == a
    _v1(client, "/desk/transfer", {"deskId": a, "targetDeskId": b}, auth_headers)
    _v1(client, "/desk/clear", {"deskId": b}, auth_headers)
    assert [x for x in _v1(client, "/desk/list", {}, auth_headers)
            if x["id"] == b][0]["status"] == 0


def test_desk_save_and_delete(client, auth_headers):
    d = _v1(client, "/desk/save",
            {"areaId": 1, "name": "T-测试台", "capacity": 4}, auth_headers)
    new_id = d["id"]
    assert [x for x in _v1(client, "/desk/list", {}, auth_headers)
            if x["id"] == new_id][0]["name"] == "T-测试台"
    _v1(client, "/desk/delete", {"deskId": new_id}, auth_headers)
    assert not [x for x in _v1(client, "/desk/list", {}, auth_headers)
                if x["id"] == new_id]


# --------------------------------------------------------------- 支付 / 报表

def test_pay_type_list(client, auth_headers):
    rows = _v1(client, "/pay/payTypeList", {}, auth_headers)
    assert {"CASH", "MEMBER", "QRCODE"} <= {r["code"] for r in rows}


def test_pay_create_and_query(client, auth_headers):
    oid = _v1_order(client, auth_headers, new_client_no("V1-PAY"))["orderId"]
    d = _v1(client, "/pay/create",
            {"orderId": oid, "payType": "QRCODE", "amount": 10.0}, auth_headers)
    assert d["needOnlinePay"] is True
    assert d["paymentId"] and d["outTradeNo"]
    q = _v1(client, "/pay/query", {"paymentId": d["paymentId"]}, auth_headers)
    assert q["paymentId"] == d["paymentId"]


def test_pay_create_unknown_order(client, auth_headers):
    b = post(client, V1 + "/pay/create",
             {"orderId": 999999, "payType": "CASH", "amount": 1.0}, auth_headers).json()
    assert b["code"] == "404"


def test_report_summary(client, auth_headers):
    d = _v1(client, "/report/summary", {"days": 7}, auth_headers)
    assert {"startDate", "endDate", "orderCount", "amountPay", "amountNet"} <= set(d)


def test_report_trends(client, auth_headers):
    for path in ("/report/salesTrend", "/report/payType", "/report/orderSource",
                 "/report/discount", "/report/productRank"):
        assert _v1(client, path, {"days": 7}, auth_headers) is not None, path


# --------------------------------------------------------------- 会员

def test_member_query_by_phone(client, auth_headers):
    d = _v1(client, "/member/query", {"phone": "13800000001"}, auth_headers)
    assert d["name"] == "张三" and d["balance"] == 500.0


def test_member_query_missing_returns_null(client, auth_headers):
    assert _v1(client, "/member/query", {"phone": "13900000000"}, auth_headers) is None


def test_member_register_duplicate(client, auth_headers):
    b = post(client, V1 + "/member/register", {"phone": "13800000001"}, auth_headers).json()
    assert b["code"] == "409"


def test_member_register_and_recharge(client, auth_headers):
    phone = "139" + new_client_no("").replace("-", "")[-8:]
    m = _v1(client, "/member/register", {"phone": phone, "name": "测试会员"}, auth_headers)
    r = _v1(client, "/member/recharge",
            {"memberId": m["memberId"], "amount": 100.0, "giftAmount": 20.0}, auth_headers)
    assert r["balance"] == 120.0
    bal = _v1(client, "/member/balance", {"memberId": m["memberId"]}, auth_headers)
    assert bal["balance"] == 120.0


def test_member_recharge_rejects_non_positive(client, auth_headers):
    b = post(client, V1 + "/member/recharge",
             {"memberId": 1, "amount": 0}, auth_headers).json()
    assert b["code"] == "400"


def test_member_level_and_rule(client, auth_headers):
    assert _v1(client, "/member/levelList", {}, auth_headers) is not None
    assert _v1(client, "/member/rechargeRule", {}, auth_headers) is not None


# --------------------------------------------------------------- 订单（新契约）

def _v1_order(client, auth_headers, cno, price=38.0, qty=2):
    return _v1(client, "/order/create",
               {"clientOrderNo": cno, "orderType": 1,
                "items": [{"productId": 1001, "productName": "宫保鸡丁",
                           "price": price, "qty": qty}]}, auth_headers)


def test_order_create_and_settle(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1"))
    order_id = d["orderId"]
    assert d["amountPay"] == 76.0 and d["status"] == 0
    pre = _v1(client, "/order/preSettle", {"orderId": order_id}, auth_headers)
    assert pre["amountPay"] == 76.0
    s = _v1(client, "/order/settle",
            {"clientOrderNo": d["clientOrderNo"], "orderId": order_id,
             "payments": [{"payType": "CASH", "amount": 76.0}]}, auth_headers)
    assert s["paid"] == 76.0 and s["change"] == 0.0
    assert _v1(client, "/order/detail", {"orderId": order_id},
               auth_headers)["status"] == 1


def test_order_create_is_idempotent(client, auth_headers):
    """同一 clientOrderNo 重复提交：409 + 原单据，不能落第二张单。"""
    cno = new_client_no("V1-IDEM")
    first = _v1_order(client, auth_headers, cno)
    b = post(client, V1 + "/order/create",
             {"clientOrderNo": cno, "orderType": 1,
              "items": [{"productId": 1001, "price": 1.0, "qty": 1}]}, auth_headers).json()
    assert b["code"] == "409"
    assert b["data"]["orderId"] == first["orderId"]
    assert b["data"]["amountPay"] == first["amountPay"]


def test_order_create_requires_client_no(client, auth_headers):
    """缺必填字段也要回标准响应体，不能漏出 FastAPI 的裸 422。"""
    r = post(client, V1 + "/order/create", {"orderType": 1}, auth_headers)
    assert r.status_code == 200
    b = r.json()
    assert b["code"] == "422" and "clientOrderNo" in b["message"]


def test_order_detail_and_page(client, auth_headers):
    oid = _v1_order(client, auth_headers, new_client_no("V1-PAGE"))["orderId"]
    page = _v1(client, "/order/page", {"pageNumber": 1, "pageSize": 50}, auth_headers)
    assert any(r["orderId"] == oid for r in page["list"])
    assert _v1(client, "/order/detail", {"orderId": oid}, auth_headers)["orderId"] == oid


def test_order_detail_unknown_returns_null(client, auth_headers):
    assert _v1(client, "/order/detail", {"orderId": 999999}, auth_headers) is None


def test_order_change_replaces_items_and_recomputes(client, auth_headers):
    oid = _v1_order(client, auth_headers, new_client_no("V1-CHG"))["orderId"]
    d = _v1(client, "/order/change",
            {"orderId": oid, "items": [{"productId": 1001, "productName": "宫保鸡丁",
                                        "price": 38.0, "qty": 1},
                                       {"productId": 1006, "productName": "米饭",
                                        "price": 2.0, "qty": 3}]}, auth_headers)
    assert len(d["items"]) == 2
    assert d["amountTotal"] == 44.0 and d["amountPay"] == 44.0


def test_order_change_rejected_after_settle(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1-CHG-S"))
    _v1(client, "/order/settle",
        {"clientOrderNo": d["clientOrderNo"], "orderId": d["orderId"],
         "payments": [{"payType": "CASH", "amount": d["amountPay"]}]}, auth_headers)
    b = post(client, V1 + "/order/change",
             {"orderId": d["orderId"], "items": []}, auth_headers).json()
    assert b["code"] == "409"


def test_order_discount_order_level_and_idempotent(client, auth_headers):
    oid = _v1_order(client, auth_headers, new_client_no("V1-DISC"))["orderId"]
    d = _v1(client, "/order/discount",
            {"orderId": oid, "target": "order", "type": "amount", "value": 6.0},
            auth_headers)
    assert d["amountDiscount"] == 6.0 and d["amountPay"] == 70.0
    # 再点一次同样的折扣，金额不能累加（重复调用必须幂等）
    again = _v1(client, "/order/discount",
                {"orderId": oid, "target": "order", "type": "amount", "value": 6.0},
                auth_headers)
    assert again["amountDiscount"] == 6.0 and again["amountPay"] == 70.0


def test_order_discount_rate_and_item_target(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1-DISC-R"))
    oid, item_id = d["orderId"], d["items"][0]["id"]
    r = _v1(client, "/order/discount",
            {"orderId": oid, "target": "order", "type": "rate", "value": 0.9},
            auth_headers)
    assert r["amountDiscount"] == 7.6 and r["amountPay"] == 68.4

    it = _v1(client, "/order/discount",
             {"orderId": oid, "target": "item", "itemId": item_id,
              "type": "amount", "value": 10.0}, auth_headers)
    # 整单折扣 + 单品折扣叠加
    assert it["amountDiscount"] == 17.6 and it["amountPay"] == 58.4
    assert _v1(client, "/order/detail", {"orderId": oid},
               auth_headers)["items"][0]["discountAmount"] == 10.0


def test_order_cancel_voids_and_releases_desk(client, auth_headers):
    free = [d for d in _v1(client, "/desk/list", {}, auth_headers) if d["status"] == 0]
    if not free:
        pytest.skip("没有空闲桌台")
    desk_id = free[0]["id"]
    d = _v1(client, "/order/create",
            {"clientOrderNo": new_client_no("V1-CANCEL"), "orderType": 1,
             "deskId": desk_id,
             "items": [{"productId": 1001, "price": 38.0, "qty": 1}]}, auth_headers)
    assert [x for x in _v1(client, "/desk/list", {}, auth_headers)
            if x["id"] == desk_id][0]["status"] == 1

    c = _v1(client, "/order/cancel", {"orderId": d["orderId"]}, auth_headers)
    assert c["status"] == 3
    assert [x for x in _v1(client, "/desk/list", {}, auth_headers)
            if x["id"] == desk_id][0]["status"] == 0


def test_order_settle_short_payment_rejected(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1-SHORT"))
    b = post(client, V1 + "/order/settle",
             {"clientOrderNo": d["clientOrderNo"], "orderId": d["orderId"],
              "payments": [{"payType": "CASH", "amount": 10.0}]}, auth_headers).json()
    assert b["code"] == "409"
    assert "支付金额不足" in b["message"]
    assert _v1(client, "/order/detail", {"orderId": d["orderId"]},
               auth_headers)["status"] == 0, "结账失败不能改单据状态"


def test_order_settle_member_deducts_balance(client, auth_headers):
    m = _v1(client, "/member/query", {"phone": "13800000001"}, auth_headers)
    oid = _v1_order(client, auth_headers, new_client_no("V1-MEM"))["orderId"]
    _v1(client, "/order/settle",
        {"clientOrderNo": "x", "orderId": oid,
         "payments": [{"payType": "MEMBER", "amount": 76.0,
                       "memberId": m["memberId"]}]}, auth_headers)
    after = _v1(client, "/member/balance", {"memberId": m["memberId"]}, auth_headers)
    assert after["balance"] == round(m["balance"] - 76.0, 2)


def test_order_settle_member_insufficient_balance(client, auth_headers):
    m = _v1(client, "/member/query", {"phone": "13900000002"}, auth_headers)
    if m is None or m["balance"] >= 76.0:
        pytest.skip("没有余额不足的会员")
    oid = _v1_order(client, auth_headers, new_client_no("V1-MEM-LOW"))["orderId"]
    b = post(client, V1 + "/order/settle",
             {"clientOrderNo": "x", "orderId": oid,
              "payments": [{"payType": "MEMBER", "amount": 76.0,
                            "memberId": m["memberId"]}]}, auth_headers).json()
    assert b["code"] == "409"


def test_order_refund_full_then_reverse_settle(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1-REF"))
    oid = d["orderId"]
    _v1(client, "/order/settle",
        {"clientOrderNo": d["clientOrderNo"], "orderId": oid,
         "payments": [{"payType": "CASH", "amount": d["amountPay"]}]}, auth_headers)

    half = _v1(client, "/order/refund", {"orderId": oid, "amount": 30.0}, auth_headers)
    assert half["amountRefund"] == 30.0 and half["status"] == 1, "部分退款仍是已结账"

    rest = _v1(client, "/order/refund", {"orderId": oid, "amount": 46.0}, auth_headers)
    assert rest["amountRefund"] == 76.0 and rest["status"] == 2

    b = post(client, V1 + "/order/refund", {"orderId": oid, "amount": 1.0},
             auth_headers).json()
    assert b["code"] == "409"

    # 已整单退款的单据不能反结账：钱已经还回顾客了，再退回「未结账」
    # 会让这单重新可结、可退款，账就对不上了。
    b = post(client, V1 + "/order/reverseSettle", {"orderId": oid},
             auth_headers).json()
    assert b["code"] == "409"


def test_order_reverse_settle_restores_pending(client, auth_headers):
    """反结账：已结账且未退款的单据回到未结账，结账时间清空。"""
    d = _v1_order(client, auth_headers, new_client_no("V1-REV"))
    oid = d["orderId"]
    _v1(client, "/order/settle",
        {"clientOrderNo": d["clientOrderNo"], "orderId": oid,
         "payments": [{"payType": "CASH", "amount": d["amountPay"]}]}, auth_headers)
    assert _v1(client, "/order/detail", {"orderId": oid},
               auth_headers)["settleTime"] != ""

    rev = _v1(client, "/order/reverseSettle", {"orderId": oid}, auth_headers)
    assert rev["status"] == 0
    assert _v1(client, "/order/detail", {"orderId": oid},
               auth_headers)["settleTime"] == ""


def test_order_refund_over_balance_rejected(client, auth_headers):
    d = _v1_order(client, auth_headers, new_client_no("V1-OVER"))
    _v1(client, "/order/settle",
        {"clientOrderNo": d["clientOrderNo"], "orderId": d["orderId"],
         "payments": [{"payType": "CASH", "amount": d["amountPay"]}]}, auth_headers)
    b = post(client, V1 + "/order/refund", {"orderId": d["orderId"], "amount": 999.0},
             auth_headers).json()
    assert b["code"] == "409"


def test_order_flow_stubs(client, auth_headers):
    """外卖相关接口一期只回占位，但要保证不报错、客户端不卡。"""
    for path in ("/order/take", "/order/storeOut", "/order/deliveryStart",
                 "/order/deliveryComplete"):
        assert _v1(client, path, {"orderId": 1}, auth_headers) is not None, path


def test_shift_new_contract_flow(client, auth_headers):
    _v1(client, "/shift/open", {"cashOpen": 0}, auth_headers)
    cur = _v1(client, "/shift/current", {}, auth_headers)
    assert cur["shiftId"]
    closed = _v1(client, "/shift/close", {"cashActual": 0}, auth_headers)
    assert closed["shiftId"] == cur["shiftId"]
    assert _v1(client, "/shift/handoverList", {}, auth_headers) is not None
