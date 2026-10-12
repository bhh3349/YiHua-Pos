"""核销 / 幂等 / 撤销 —— 任务卡 4.3。"""
from datetime import datetime, timedelta

import pytest

from app.services.groupbuy import guard
from conftest import post

GB = "/api/v1/groupbuy"


def _setup(client, auth_headers):
    r = post(client, GB + "/configSave",
             {"platformCode": "self", "storeIdList": [200001],
              "signKey": "k" * 20}, auth_headers).json()
    assert r["code"] == "200", r


def _issue(client, auth_headers, code, discount="99.00", **kw):
    body = {"couponCode": code, "discountAmount": discount,
            "faceValue": kw.pop("faceValue", discount),
            "validFrom": kw.pop("validFrom", ""),
            "validTo": kw.pop("validTo", "")}
    body.update(kw)
    r = post(client, GB + "/coupon/issue", body, auth_headers).json()
    assert r["code"] == "200", r


def _redeem(client, auth_headers, code, order_id=0, discount=None, platform="self"):
    body = {"platformCode": platform, "couponCode": code, "storeId": 200001,
            "orderId": order_id}
    if discount is not None:
        body["actualDiscount"] = discount
    return post(client, GB + "/coupon/redeem", body, auth_headers).json()


def _reverse(client, auth_headers, code, reason="操作失误"):
    return post(client, GB + "/coupon/reverse",
                {"platformCode": "self", "couponCode": code,
                 "storeId": 200001, "reason": reason}, auth_headers).json()


@pytest.fixture()
def gb(client, auth_headers):
    guard.reset_rate_limit()      # 限流计数是进程级的，逐用例清零
    _setup(client, auth_headers)
    return client, auth_headers


def test_redeem_success(gb):
    client, h = gb
    _issue(client, h, "RDM00001")
    b = _redeem(client, h, "RDM00001")
    assert b["code"] == "200", b
    d = b["data"]
    assert d["success"] is True
    assert d["actualDiscount"] == "99.00"


def test_redeem_twice_second_fails(gb):
    """同一张券核销两次，第二次必须失败（资金安全）。"""
    client, h = gb
    _issue(client, h, "RDM00002")
    assert _redeem(client, h, "RDM00002")["code"] == "200"
    dup = _redeem(client, h, "RDM00002")
    assert dup["code"] == "COUPON_USED", dup


def test_redeem_actual_discount_validation(gb):
    """实际抵扣：负数拒、超券面拒、合法让利通过。"""
    client, h = gb
    _issue(client, h, "RDM00003")
    assert _redeem(client, h, "RDM00003", discount="-1")["code"] == "400"
    assert _redeem(client, h, "RDM00003", discount="100.00")["code"] == "400"
    assert _redeem(client, h, "RDM00003", discount="59.00")["code"] == "200"


def test_redeem_with_local_order(gb):
    """方式 A：核销关联本地订单，订单必须存在且属于本门店。"""
    from conftest import envelope, new_client_no
    client, h = gb
    # 建一单本地订单（用新契约 order/create）
    o = post(client, "/api/v1/order/create",
             {"clientOrderNo": new_client_no("GB"), "orderType": 1,
              "items": [{"productId": 1001, "productName": "宫保鸡丁",
                         "price": 38.0, "qty": 1}]}, h).json()
    assert o["code"] == "200", o
    order_id = o["data"]["orderId"]

    _issue(client, h, "RDM00004")
    bad = _redeem(client, h, "RDM00004", order_id=999999)
    assert bad["code"] == "404", bad
    ok = _redeem(client, h, "RDM00004", order_id=order_id)
    assert ok["code"] == "200", ok


def test_redeem_log_records_everything(gb):
    """核销记录：谁、什么时候、哪个订单、抵了多少。"""
    client, h = gb
    _issue(client, h, "RDM00005")
    assert _redeem(client, h, "RDM00005")["code"] == "200"
    r = post(client, GB + "/redeemLog",
             {"couponCode": "RDM00005"}, h).json()
    assert r["code"] == "200", r
    lst = r["data"]["list"]
    assert len(lst) == 1, lst
    item = lst[0]
    assert item["couponCode"] == "RDM00005"
    assert item["action"] == "redeem"
    assert item["operatorId"] == 1
    assert item["actualDiscount"] == "99.00"
    assert item["createdAt"]


def test_reverse_then_redeem_again(gb):
    """撤销核销后券回到待核销，可重新核销。"""
    client, h = gb
    _issue(client, h, "RDM00006")
    assert _redeem(client, h, "RDM00006")["code"] == "200"

    # 撤销必须带原因
    no_reason = post(client, GB + "/coupon/reverse",
                     {"platformCode": "self", "couponCode": "RDM00006",
                      "storeId": 200001, "reason": ""}, h).json()
    assert no_reason["code"] == "REVERSE_REASON_REQUIRED", no_reason

    rv = _reverse(client, h, "RDM00006", reason="客人退套餐")
    assert rv["code"] == "200", rv

    # 撤销后可重新核销
    again = _redeem(client, h, "RDM00006")
    assert again["code"] == "200", again
    # 再撤销一次也行（第二次核销同样可撤）
    assert _reverse(client, h, "RDM00006")["code"] == "200"


def test_reverse_unused_coupon_rejected(gb):
    client, h = gb
    _issue(client, h, "RDM00007")
    b = _reverse(client, h, "RDM00007")
    assert b["code"] == "ALREADY_REVERSED", b


def test_reverse_log_records_reason(gb):
    client, h = gb
    _issue(client, h, "RDM00008")
    assert _redeem(client, h, "RDM00008")["code"] == "200"
    assert _reverse(client, h, "RDM00008", reason="输错券码")["code"] == "200"
    r = post(client, GB + "/redeemLog", {"couponCode": "RDM00008"}, h).json()
    actions = {x["action"]: x for x in r["data"]["list"]}
    assert actions["reverse"]["reason"] == "输错券码", actions["reverse"]
    assert actions["reverse"]["operatorId"] == 1, actions["reverse"]


def test_redeem_unknown_platform_rejected(gb):
    client, h = gb
    _issue(client, h, "RDM00009")
    b = _redeem(client, h, "RDM00009", platform="nosuch")
    assert b["code"] == "PLATFORM_UNKNOWN", b


def test_redeem_skeleton_platform_rejected(gb):
    """未接入平台（骨架 adapter）报「平台未接入」，不是静默成功。"""
    client, h = gb
    post(client, GB + "/configSave",
         {"platformCode": "douyin", "storeIdList": [200001],
          "signKey": "k" * 20}, h)
    b = _redeem(client, h, "RDM00009", platform="douyin")
    assert b["code"] == "PLATFORM_NOT_INTEGRATED", b


def test_redeem_requires_config(gb):
    """没配置平台就核销 -> CONFIG_NOT_FOUND。"""
    from app.db import SessionLocal
    from app.models_groupbuy import GroupbuyPlatformConfig
    client, h = gb
    db = SessionLocal()
    try:
        db.query(GroupbuyPlatformConfig).delete()
        db.commit()
    finally:
        db.close()
    b = _redeem(client, h, "RDM00010")
    assert b["code"] == "CONFIG_NOT_FOUND", b


def test_redeem_store_not_bound(gb):
    """平台绑定了门店 A，用门店 B 核销 -> 门店不匹配。"""
    client, h = gb
    post(client, GB + "/configSave",
         {"platformCode": "self", "storeIdList": [200001],
          "signKey": "k" * 20}, h)
    r = post(client, GB + "/coupon/redeem",
             {"platformCode": "self", "couponCode": "RDM00011",
              "storeId": 300001}, h).json()
    assert r["code"] == "COUPON_STORE_MISMATCH", r
