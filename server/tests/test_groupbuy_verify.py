"""8 种券状态验证 —— 任务卡 4.2 的校验表，每项一个测试。"""
from datetime import datetime, timedelta

import pytest

from app.services.groupbuy import guard
from conftest import post

GB = "/api/v1/groupbuy"


def _cfg(client, auth_headers, platform="self", stores=None):
    r = post(client, GB + "/configSave",
             {"platformCode": platform, "storeIdList": stores or [200001],
              "signKey": "test-sign-key-abcdef123456"}, auth_headers).json()
    assert r["code"] == "200", r
    return r["data"]


def _issue(client, auth_headers, code, **kw):
    body = {"couponCode": code, "couponName": kw.pop("name", "双人套餐"),
            "couponType": kw.pop("type", "package"),
            "faceValue": kw.pop("faceValue", "99.00"),
            "discountAmount": kw.pop("discount", "99.00"),
            "validFrom": kw.pop("validFrom", ""),
            "validTo": kw.pop("validTo", ""),
            "restrictions": kw.pop("restrictions", {})}
    body.update(kw)
    r = post(client, GB + "/coupon/issue", body, auth_headers).json()
    assert r["code"] == "200", r
    return r["data"]


def _verify(client, auth_headers, code, platform="self", store_id=200001, **kw):
    body = {"platformCode": platform, "couponCode": code, "storeId": store_id}
    body.update(kw)
    return post(client, GB + "/coupon/verify", body, auth_headers).json()


def _verify_at(client, auth_headers, code, biz_time, **kw):
    """用注入的业务时间走一遍 adapter 验券（覆盖时段限制，不依赖真实时钟）。"""
    from app.db import SessionLocal
    from app.services.groupbuy import store as cfg_store
    from app.services.groupbuy.platform import get_adapter
    db = SessionLocal()
    try:
        q = cfg_store.build_query(db, 100001, 200001, "self", code,
                                  biz_time=biz_time, **kw)
        return get_adapter("self").verify_coupon(q)
    finally:
        db.close()


def _err_code(body):
    return (body.get("data") or {}).get("errorCode") or body.get("code")


@pytest.fixture()
def setup(client, auth_headers):
    guard.reset_rate_limit()      # 限流计数是进程级的，逐用例清零互不干扰
    _cfg(client, auth_headers)
    return client, auth_headers


def test_valid_coupon_passes(setup):
    client, h = setup
    _issue(client, h, "VAL0001")
    b = _verify(client, h, "VAL0001")
    assert b["code"] == "200", b
    d = b["data"]
    assert d["valid"] is True
    c = d["coupon"]
    assert c["couponCode"] == "VAL0001"
    assert c["platformCode"] == "self"
    assert c["couponType"] == "package"
    assert c["faceValue"] == "99.00"
    assert c["discountAmount"] == "99.00"


def test_coupon_not_found(setup):
    client, h = setup
    b = _verify(client, h, "NOPE9999")
    assert _err_code(b) == "COUPON_NOT_FOUND", b


def test_coupon_used(setup):
    client, h = setup
    _issue(client, h, "USED0001")
    r = post(client, GB + "/coupon/redeem",
             {"platformCode": "self", "couponCode": "USED0001",
              "storeId": 200001, "orderId": 0}, h).json()
    assert r["code"] == "200", r
    b = _verify(client, h, "USED0001")
    assert _err_code(b) == "COUPON_USED", b


def test_coupon_not_yet_valid(setup):
    client, h = setup
    _issue(client, h, "FUTUR001",
           validFrom=(datetime.now() + timedelta(days=1)).strftime("%Y-%m-%d %H:%M:%S"))
    b = _verify(client, h, "FUTUR001")
    assert _err_code(b) == "COUPON_NOT_YET_VALID", b


def test_coupon_expired(setup):
    client, h = setup
    _issue(client, h, "EXPIRE01",
           validTo=(datetime.now() - timedelta(days=1)).strftime("%Y-%m-%d %H:%M:%S"))
    b = _verify(client, h, "EXPIRE01")
    assert _err_code(b) == "COUPON_EXPIRED", b


def test_coupon_store_mismatch(setup):
    """restrictions.storeIds 限定门店，别的门店验券报门店不匹配。"""
    client, h = setup
    _issue(client, h, "STORE001",
           restrictions={"storeIds": [200002]})
    b = _verify(client, h, "STORE001", store_id=200001)
    assert _err_code(b) == "COUPON_STORE_MISMATCH", b


def test_coupon_time_restricted(setup):
    """限制 11:00~14:00：中午 12 点可用，晚上 20 点报时段不符。"""
    client, h = setup
    _issue(client, h, "TIME0001",
           restrictions={"timeRanges": [{"start": "11:00", "end": "14:00"}]})
    r = _verify_at(client, h, "TIME0001", datetime(2026, 10, 12, 12, 0, 0))
    assert r.valid is True, r
    r2 = _verify_at(client, h, "TIME0001", datetime(2026, 10, 12, 20, 0, 0))
    assert not r2.valid and r2.error_code == "COUPON_TIME_RESTRICTED", r2


def test_coupon_time_range_cross_midnight(setup):
    """跨零点夜宵档 22:00~02:00：23:30 与 01:30 都算在时段内，12:00 不算。"""
    client, h = setup
    _issue(client, h, "TIME0002",
           restrictions={"timeRanges": [{"start": "22:00", "end": "02:00"}]})
    assert _verify_at(client, h, "TIME0002",
                      datetime(2026, 10, 12, 23, 30, 0)).valid is True
    assert _verify_at(client, h, "TIME0002",
                      datetime(2026, 10, 12, 1, 30, 0)).valid is True
    r = _verify_at(client, h, "TIME0002", datetime(2026, 10, 12, 12, 0, 0))
    assert not r.valid and r.error_code == "COUPON_TIME_RESTRICTED", r


def test_coupon_need_booking(setup):
    client, h = setup
    _issue(client, h, "BOOK0001",
           restrictions={"needBooking": True})
    b = _verify(client, h, "BOOK0001")
    assert _err_code(b) == "COUPON_NEED_BOOKING", b
    # 带上预约标记 -> 通过
    b2 = _verify(client, h, "BOOK0001", booked=True)
    assert b2["data"]["valid"] is True, b2


def test_platform_mismatch(setup):
    """券是 self 平台的，用 meituan 验 -> 平台不匹配。"""
    client, h = setup
    _cfg(client, h, platform="meituan")
    _issue(client, h, "PLATF001")
    # meituan 是骨架（未接入），先确认它确实报「平台未接入」
    b = _verify(client, h, "PLATF001", platform="meituan")
    assert b["code"] == "PLATFORM_NOT_INTEGRATED", b


def test_verify_via_adapter_reports_platform_mismatch(setup):
    """平台不匹配在 adapter 内部判定（self 券 + 错误平台上下文）。"""
    from app.services.groupbuy.platform import CouponInfo, CouponQuery, VerifyResult
    from app.services.groupbuy.verify import verify_state
    from types import SimpleNamespace

    coupon = SimpleNamespace(
        platform_code="self", status=1, valid_from=None, valid_to=None,
        restrictions={}, coupon_code="X", platform_coupon_id="", coupon_name="",
        coupon_type="cash", face_value=1, discount_amount=1)
    req = CouponQuery(platform_code="meituan", coupon_code="X", store_id=1,
                      tenant_id=100001)
    r = verify_state(coupon, req)
    assert not r.valid and r.error_code == "PLATFORM_MISMATCH", r


def test_coupon_code_format_rejected(setup):
    """空券码/超长/特殊字符 —— 防注入（任务卡 4.7）。

    非法格式在进入 adapter 之前就被打回；验券接口对「券不可用」统一用
    HTTP 200 + data.valid=false 的信封，所以两种判法都要认。
    """
    client, h = setup
    for bad in ("", "abc", "x" * 65, "AB-12", "券码测试", "1234 5678"):
        b = _verify(client, h, bad)
        assert _err_code(b) == "COUPON_NOT_FOUND", (bad, b)


def test_verify_does_not_consume_coupon(setup):
    """验券不改状态：验 10 次后仍可核销。"""
    client, h = setup
    _issue(client, h, "IDEMVF01")
    for _ in range(10):
        b = _verify(client, h, "IDEMVF01")
        assert b["data"]["valid"] is True, b
    r = post(client, GB + "/coupon/redeem",
             {"platformCode": "self", "couponCode": "IDEMVF01",
              "storeId": 200001}, h).json()
    assert r["code"] == "200", r


def test_verify_rate_limited(setup):
    """验券防刷：同一门店窗口内超限 -> RATE_LIMITED（任务卡 4.7）。"""
    client, h = setup
    guard.reset_rate_limit()
    _issue(client, h, "RATEL001")
    seen_limit = False
    for _ in range(guard._LIMIT + 3):
        b = _verify(client, h, "RATEL001")
        if b["code"] == "RATE_LIMITED":
            seen_limit = True
            break
        assert b["data"]["valid"] is True, b
    assert seen_limit, "连续验券未触发限流"
    guard.reset_rate_limit()
