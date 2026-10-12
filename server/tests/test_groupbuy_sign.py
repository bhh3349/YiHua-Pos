"""回调验签 + 密钥不落日志/响应 —— 任务卡 4.5。

自有平台回调签名规则：payload 里非空字段按 key 排序拼 ``k=v&...``，
``sign = hmac_sha256_hex(sign_key, text)``；服务端常数时间比较。
"""
import hashlib
import hmac as hmac_mod

import pytest

from conftest import post

GB = "/api/v1/groupbuy"
SIGN_KEY = "test-sign-key-abcdef123456"


def _setup(client, auth_headers):
    r = post(client, GB + "/configSave",
             {"platformCode": "self", "storeIdList": [200001],
              "signKey": SIGN_KEY}, auth_headers).json()
    assert r["code"] == "200", r


@pytest.fixture()
def gb(client, auth_headers):
    _setup(client, auth_headers)
    return client, auth_headers


def _sign(payload: dict, key: str = SIGN_KEY) -> str:
    items = sorted((k, v) for k, v in payload.items()
                   if v is not None and v != "")
    text = "&".join("%s=%s" % (k, v) for k, v in items)
    return hmac_mod.new(key.encode("utf-8"), text.encode("utf-8"),
                        hashlib.sha256).hexdigest()


def _callback(client, h, payload, sign=None):
    return post(client, GB + "/callback",
                {"platformCode": "self", "payload": payload,
                 "sign": sign if sign is not None else _sign(payload)}, h).json()


def test_callback_valid_sign_accepted(gb):
    client, h = gb
    payload = {"event": "coupon.used", "couponCode": "CB00001", "ts": "1697000000"}
    b = _callback(client, h, payload)
    assert b["code"] == "200", b
    assert b["data"]["accepted"] is True


def test_callback_bad_sign_rejected(gb):
    client, h = gb
    payload = {"event": "coupon.used", "couponCode": "CB00002"}
    b = _callback(client, h, payload, sign="0" * 64)
    assert b["code"] == "SIGN_INVALID", b


def test_callback_missing_sign_rejected(gb):
    client, h = gb
    b = post(client, GB + "/callback",
             {"platformCode": "self",
              "payload": {"event": "x"}, "sign": ""}, h).json()
    assert b["code"] == "SIGN_INVALID", b


def test_callback_tampered_payload_rejected(gb):
    """签名对 payload 绑定：改一个字段就过不了验签。"""
    client, h = gb
    payload = {"event": "coupon.used", "couponCode": "CB00003"}
    b = _callback(client, h, payload)
    assert b["code"] == "200"
    tampered = dict(payload, couponCode="CB99999")
    b2 = _callback(client, h, tampered, sign=_sign(payload))
    assert b2["code"] == "SIGN_INVALID", b2


def test_config_response_never_contains_plaintext_key(client, auth_headers):
    """配置接口只回掩码，明文密钥不许出现在任何响应里。"""
    _setup(client, auth_headers)
    raw = post(client, GB + "/configList", {}, auth_headers)
    text = raw.text
    assert SIGN_KEY not in text, "明文密钥泄露到 configList 响应"
    row = raw.json()["data"][0]
    assert row["hasSignKey"] is True
    assert row["signKeyMasked"] and SIGN_KEY not in row["signKeyMasked"]
    assert "*" in row["signKeyMasked"], row


def test_sign_error_message_never_echoes_key(gb):
    """验签失败的响应里不许回显密钥或签名原文。"""
    client, h = gb
    b = post(client, GB + "/callback",
             {"platformCode": "self", "payload": {"a": "1"},
              "sign": "bad"}, h)
    text = b.text
    assert SIGN_KEY not in text, "密钥泄露到错误响应"
    assert "gbs1." not in text, "密文格式泄露到错误响应"


def test_crypto_roundtrip_and_mask():
    """密文可解回原文；掩码只留首尾。"""
    from app.services.groupbuy.crypto import decrypt_secret, encrypt_secret, mask_secret
    cipher = encrypt_secret(SIGN_KEY)
    assert SIGN_KEY not in cipher
    assert decrypt_secret(cipher) == SIGN_KEY
    m = mask_secret(SIGN_KEY)
    assert SIGN_KEY not in m and m.startswith("te") and m.endswith("56"), m
