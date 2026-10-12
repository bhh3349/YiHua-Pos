"""对账 —— 任务卡 4.5：差异能算出来、能导出。

差异口径：匹配键 = 券码；已撤销的核销两侧都不计（否则天天报假差异）。
金额全程 Decimal，落地为字符串。
"""
from datetime import datetime
from decimal import Decimal

import pytest

from app.services.groupbuy import reconciliation
from app.services.groupbuy.platform import PlatformRedeemRecord
from conftest import post

GB = "/api/v1/groupbuy"
DAY = datetime.now().strftime("%Y-%m-%d")      # 核销发生在「今天」，对账也查今天


def _setup(client, auth_headers):
    r = post(client, GB + "/configSave",
             {"platformCode": "self", "storeIdList": [200001],
              "signKey": "k" * 20}, auth_headers).json()
    assert r["code"] == "200", r


@pytest.fixture()
def gb(client, auth_headers):
    _setup(client, auth_headers)
    _wipe_coupons()               # DB 是 session 级的，上一条用例的券会串进来
    return client, auth_headers


def _wipe_coupons():
    from app.db import SessionLocal
    from app.models_groupbuy import GroupbuyCoupon, GroupbuyRedeemLog
    db = SessionLocal()
    try:
        db.query(GroupbuyRedeemLog).delete()
        db.query(GroupbuyCoupon).delete()
        db.commit()
    finally:
        db.close()


def _issue_and_redeem(client, h, code, amount="50.00"):
    r = post(client, GB + "/coupon/issue",
             {"couponCode": code, "discountAmount": amount,
              "faceValue": amount}, h).json()
    assert r["code"] == "200", r
    r = post(client, GB + "/coupon/redeem",
             {"platformCode": "self", "couponCode": code,
              "storeId": 200001, "orderId": 0}, h).json()
    assert r["code"] == "200", r


class _StubAdapter:
    """假平台：核销记录由用例脚本给定，用来制造各种差异。"""

    platform_code = "self"

    def __init__(self, records):
        self._records = records

    def reconcile(self, req, day):
        return list(self._records)


def _patch_platform(monkeypatch, records):
    monkeypatch.setattr(reconciliation, "get_adapter",
                        lambda code: _StubAdapter(records))


def _rec(code, amount, order_no="P100"):
    return PlatformRedeemRecord(platform_code="self", coupon_code=code,
                                redeem_time=datetime.now(),
                                discount_amount=Decimal(amount), order_no=order_no)


def _reconcile(client, h):
    return post(client, GB + "/reconcile",
                {"platformCode": "self", "date": DAY, "storeId": 200001}, h).json()


def _by_code(result):
    return {i["couponCode"]: i for i in result["data"]["items"]}


def test_reconcile_all_match(gb, monkeypatch):
    client, h = gb
    _issue_and_redeem(client, h, "REC00001", "50.00")
    _issue_and_redeem(client, h, "REC00002", "30.00")
    _patch_platform(monkeypatch, [_rec("REC00001", "50.00"),
                                  _rec("REC00002", "30.00")])

    r = _reconcile(client, h)
    assert r["code"] == "200", r
    s = r["data"]["summary"]
    assert s["matchCount"] == 2 and s["diffCount"] == 0, s
    assert s["localAmount"] == "80.00" and s["platformAmount"] == "80.00", s
    assert s["amountGap"] == "0.00", s
    assert all(i["status"] == "MATCH" for i in r["data"]["items"])


def test_reconcile_amount_diff(gb, monkeypatch):
    """两边都有该券但金额不一致 -> AMOUNT_DIFF，差额算得出来。"""
    client, h = gb
    _issue_and_redeem(client, h, "REC00003", "50.00")
    _patch_platform(monkeypatch, [_rec("REC00003", "40.00")])

    r = _reconcile(client, h)
    s = r["data"]["summary"]
    assert s["diffCount"] == 1, s
    assert s["amountGap"] == "10.00", s          # 本地多抵了 10 元
    item = _by_code(r)["REC00003"]
    assert item["status"] == "AMOUNT_DIFF"
    assert item["statusName"] == "金额不符"
    assert item["localAmount"] == "50.00" and item["platformAmount"] == "40.00"


def test_reconcile_local_only(gb, monkeypatch):
    """本地有、平台没有 -> LOCAL_ONLY（可能我们重复核销/平台漏记）。"""
    client, h = gb
    _issue_and_redeem(client, h, "REC00004", "20.00")
    _patch_platform(monkeypatch, [])

    r = _reconcile(client, h)
    s = r["data"]["summary"]
    assert s["localCount"] == 1 and s["platformCount"] == 0, s
    assert s["diffCount"] == 1, s
    item = _by_code(r)["REC00004"]
    assert item["status"] == "LOCAL_ONLY"
    assert item["platformAmount"] is None
    diffs = r["data"]["diffItems"]
    assert [i["couponCode"] for i in diffs] == ["REC00004"]


def test_reconcile_platform_only(gb, monkeypatch):
    """平台有、本地没有 -> PLATFORM_ONLY（漏单，最该查的一种）。"""
    client, h = gb
    _patch_platform(monkeypatch, [_rec("REC00005", "99.00", order_no="PT999")])

    r = _reconcile(client, h)
    s = r["data"]["summary"]
    assert s["localCount"] == 0 and s["platformCount"] == 1, s
    item = _by_code(r)["REC00005"]
    assert item["status"] == "PLATFORM_ONLY"
    assert item["localAmount"] is None
    assert item["platformAmount"] == "99.00"
    assert item["platformOrderNo"] == "PT999"


def test_reconcile_excludes_reversed(gb, monkeypatch):
    """撤销过的核销两侧都不计 —— 否则每天都会报一笔假差异。"""
    client, h = gb
    _issue_and_redeem(client, h, "REC00006", "60.00")
    rv = post(client, GB + "/coupon/reverse",
              {"platformCode": "self", "couponCode": "REC00006",
               "storeId": 200001, "reason": "客人退单"}, h).json()
    assert rv["code"] == "200", rv

    # 平台侧（真实 self adapter）也应看不到这笔
    r = _reconcile(client, h)
    s = r["data"]["summary"]
    assert s["localCount"] == 0, s
    assert s["diffCount"] == 0, s
    assert r["data"]["items"] == []


def test_reconcile_money_is_string_not_float(gb, monkeypatch):
    """所有金额字段必须是字符串（避免 JSON float 精度丢失）。"""
    client, h = gb
    _issue_and_redeem(client, h, "REC00007", "0.10")
    _patch_platform(monkeypatch, [_rec("REC00007", "0.10")])
    r = _reconcile(client, h)
    s = r["data"]["summary"]
    for k in ("localAmount", "platformAmount", "diffAmount", "amountGap"):
        assert isinstance(s[k], str), (k, s[k])
    item = _by_code(r)["REC00007"]
    for k in ("localAmount", "platformAmount", "diffAmount"):
        assert isinstance(item[k], str), (k, item[k])


def test_csv_export_has_bom(tmp_path, gb, monkeypatch):
    """导出 CSV 必须是 UTF-8 BOM 开头，Excel 打开中文才不乱码。"""
    client, h = gb
    _issue_and_redeem(client, h, "REC00008", "10.00")
    _patch_platform(monkeypatch, [_rec("REC00008", "10.00")])

    from app.db import SessionLocal
    from app.services.groupbuy import store as cfg_store
    db = SessionLocal()
    try:
        q = cfg_store.build_query(db, 100001, 200001, "self", "")
        result = reconciliation.reconcile(db, q, datetime.now(), store_id=200001)
        path = reconciliation.export_csv(
            result, str(tmp_path / "reconcile.csv"))
    finally:
        db.close()

    raw = open(path, "rb").read()
    assert raw[:3] == b"\xef\xbb\xbf", raw[:8]
    text = raw.decode("utf-8-sig")
    assert "券码" in text and "REC00008" in text
