"""响应格式契约 —— 两种格式都必须与真实抓包一致。

格式一 ResponseBase：{"code":200,"msg":"成功","data":{对象}}
格式二 ApiResponse ：{"code":200,...,"data":"<二次编码的 JSON 字符串>"}
"""
import json

from conftest import envelope_raw, post

# ------------------------------------------------------------ 格式一

FORMAT1_PATHS = [
    ("/saas/login", {"userName": "admin", "password": "123456", "loginType": "1"}),
    ("/saas/loginConfirm", {"userId": 1}),
    ("/store/getStoreConfig/", {}),
]


def test_format1_code_is_int_200(client):
    for path, body in FORMAT1_PATHS:
        body_json = post(client, path, body).json()
        assert body_json["code"] == 200, (path, body_json)
        assert not isinstance(body_json["code"], str), path


def test_format1_data_is_object_not_string(client):
    """登录/门店配置的 data 必须是对象本身，不能二次编码。"""
    for path, body in FORMAT1_PATHS:
        body_json = post(client, path, body).json()
        assert isinstance(body_json["data"], dict), (path, body_json["data"])


def test_format1_has_no_success_or_trace(client):
    body = post(client, "/saas/login", {"userName": "admin"}).json()
    assert "success" not in body
    assert "trace_no" not in body
    assert "key_sign" not in body


def test_format1_msg(client):
    body = post(client, "/saas/login", {"userName": "admin"}).json()
    assert body["msg"] == "成功."
    assert body["message"] == body["msg"]


def test_refresh_jwt_is_format1_with_string_data(client):
    body = client.get("/saas/refreshJwt").json()
    assert body["code"] == 200
    assert isinstance(body["data"], str) and len(body["data"]) >= 32


def test_logout_format1(client):
    body = post(client, "/saas/logout").json()
    assert body["code"] == 200


def test_login_payload_shape(client):
    """客户端靠这些字段过登录校验，缺一个就卡在登录页。"""
    d = post(client, "/saas/login", {"userName": "admin"}).json()["data"]
    for key in ("loginToken", "refreshToken", "merchantNo", "terminalNo",
                "accountName", "storeId", "storeCode", "storeName"):
        assert d.get(key), key
    assert len(d["loginToken"]) >= 32, "客户端要求 keyVector >= 32"
    assert d["user_info"]["tradeType"] >= 2, "tradeType < 2 客户端会判定为非餐饮产品"
    assert d["user_info"]["storeMode"] in (1, 2, 3)


# ------------------------------------------------------------ 格式二

FORMAT2_PATHS = [
    ("/pos/area/list", {}),
    ("/pos/desk/list", {}),
    ("/pos/store/info", {}),
    ("/pos/operate/getOperatelist", {}),
    ("/pos/surcharge/query", {}),
    ("/pos/print/sync/template", {}),
    ("/open/print/device/brandList", {}),
    ("/open/store/queryAbilityOpen", {}),
]


def test_format2_outer_shape(client):
    for path, body in FORMAT2_PATHS:
        b = post(client, path, body).json()
        assert b["code"] == 200, (path, b)
        assert b["success"] is True, path
        assert "key_sign" in b and "trace_no" in b, path
        assert b["msg"] == b["message"], path


def test_format2_data_is_double_encoded_string(client):
    """格式二的 data 是 JSON 字符串，客户端要 json.loads 两次。"""
    for path, body in FORMAT2_PATHS:
        b = post(client, path, body).json()
        assert isinstance(b["data"], str), (path, type(b["data"]))
        parsed = json.loads(b["data"])          # 第二次解码必须成功
        assert parsed is not None, path


def test_format2_business_error_keeps_http_200(client):
    """业务失败：HTTP 仍是 200，靠 body.code 表达（客户端只看 code）。"""
    r = post(client, "/pos/order/detail", {"orderNo": "NOT-EXIST-XXX"})
    assert r.status_code == 200
    b = r.json()
    assert b["code"] == 404 and b["success"] is False
    assert b["data"] is None


def test_format2_unicode_not_escaped(client):
    """中文必须原样输出，客户端按 UTF-8 解。"""
    raw = post(client, "/pos/area/list", {}).text
    assert "大厅" in raw


def test_request_accepts_double_encoded_body(client):
    """旧客户端把入参也放在 data 字符串里，服务端要能解。"""
    flat = post(client, "/pos/desk/list", {})
    wrapped = post(client, "/pos/desk/list",
                   {"data": json.dumps({"storeId": 200001})})
    assert flat.json()["code"] == 200
    assert wrapped.json()["code"] == 200
    assert json.loads(wrapped.json()["data"]) == json.loads(flat.json()["data"])


def test_catch_all_returns_envelope(client):
    """未实现的参考系统接口一律回成功空数据，避免客户端登录链中断。"""
    b = post(client, "/some/unimplemented/legacy/api", {}).json()
    assert b["code"] == 200 and b["success"] is True
    assert json.loads(b["data"]) == {}


def test_catch_all_does_not_shadow_new_contract(client):
    b = post(client, "/api/v1/not/here", {}).json()
    assert b["code"] == 404


def test_envelope_raw_helper_contract(client):
    """兜底自检：envelope_raw 拿到的是原始字符串，不是 dict。"""
    raw = envelope_raw(post(client, "/pos/store/info", {}))
    assert isinstance(raw["data"], str)
