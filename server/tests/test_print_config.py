"""打印模板 / 打印设备 / 打印策略 / 功能开关 / 商户授权（P0-6、P0-7）。"""
from conftest import envelope, post

RECEIPT_TYPES = {1: "预结单", 2: "结账单", 3: "交班单", 4: "退菜单", 5: "充值单",
                 6: "会员消费单", 7: "反结账单", 8: "点菜单", 9: "外送单", 10: "挂账单"}


# --------------------------------------------------------------- 模板

def test_sync_template_top_keys(client):
    d = envelope(post(client, "/pos/print/sync/template", {}))
    assert set(d) == {"tagTemplateList", "receiptTemplateList",
                      "storeTemplateConfigList", "receiptOrderTemplateList"}


def test_tag_template(client):
    d = envelope(post(client, "/pos/print/sync/template", {}))
    tags = d["tagTemplateList"]
    assert len(tags) == 1
    assert tags[0]["templateId"] == "TAG0001"
    assert {"storeName", "deskNameSwitch", "food", "specification", "remarkSwitch",
            "storeId", "tenantId"} <= set(tags[0])


def test_receipt_template_list(client):
    d = envelope(post(client, "/pos/print/sync/template", {}))
    rows = d["receiptTemplateList"]
    assert len(rows) == 10
    assert {r["templateType"]: r["templateName"] for r in rows} == RECEIPT_TYPES
    assert all(r["storeId"] == 200001 and r["tenantId"] == 100001 for r in rows)


def test_store_template_config_matches_receipt(client):
    d = envelope(post(client, "/pos/print/sync/template", {}))
    receipt_ids = {r["templateId"] for r in d["receiptTemplateList"]}
    cfg_ids = {c["templateId"] for c in d["storeTemplateConfigList"]}
    assert receipt_ids == cfg_ids


def test_order_template_has_welcome_and_amounts(client):
    d = envelope(post(client, "/pos/print/sync/template", {}))
    rows = d["receiptOrderTemplateList"]
    assert len(rows) == 10
    r0 = rows[0]
    assert r0["welcomeMessage"] == "欢迎光临"
    assert {"totalAmountSwitch", "paidAmount", "payTypeSwitch", "foodSort",
            "foodSpace", "discountSwitch", "specification"} <= set(r0)


def test_template_persisted_per_store(client):
    """模板写进 dictionary_table，第二次读必须一模一样。"""
    a = envelope(post(client, "/pos/print/sync/template", {}))
    b = envelope(post(client, "/pos/print/sync/template", {}))
    assert a == b


# --------------------------------------------------------------- 设备 / 策略

def test_sync_device_top_keys(client):
    d = envelope(post(client, "/pos/print/sync/device", {}))
    assert set(d) == {"printStrategyList", "deviceList"}


def test_default_strategy_fields(client):
    d = envelope(post(client, "/pos/print/sync/device", {}))
    s = d["printStrategyList"][0]
    for key in ("strategyName", "strategyType", "defaultStrategySwitch",
                "deskIds", "addFoodSwitch", "refundFoodSwitch", "payPrintNum",
                "modifierPrintMode", "storeId", "tenantId"):
        assert key in s, key
    assert "deviceList" not in s, "抓包里 printStrategyList 元素不含 deviceList"
    assert s["strategyName"] == "后厨档口"


def test_builtin_device(client):
    d = envelope(post(client, "/pos/print/sync/device", {}))
    dev = d["deviceList"][0]
    assert dev["deviceId"] == "LOCAL001"
    assert dev["deviceName"] == "收银机内置打印机"
    assert dev["paperWidth"] == 58
    assert dev["storeId"] == 200001


def test_brand_list(client):
    rows = envelope(post(client, "/open/print/device/brandList", {}))
    assert len(rows) == 5
    assert {"芯烨", "佳博", "其他", "汉印", "大趋智能"} == {r["brand_name"] for r in rows}
    assert all(isinstance(r["brand_id"], int) for r in rows)


def test_device_list_matches_snapshot(client):
    snapshot = envelope(post(client, "/pos/print/sync/device", {}))["deviceList"]
    assert envelope(post(client, "/open/print/device/list", {})) == snapshot


def test_strategy_list_subset_fields(client):
    rows = envelope(post(client, "/pos/print/strategy/list", {}))
    assert len(rows) == 1
    assert set(rows[0]) == {"defaultStrategySwitch", "strategyName", "deviceList",
                            "strategyId", "modifierPrintMode", "bilTypeList",
                            "strategyType"}
    assert rows[0]["bilTypeList"] == [3, 4, 13]
    assert rows[0]["deviceList"][0]["deviceId"] == "LOCAL001"
    assert len(rows[0]["strategyId"]) == 24


def test_strategy_list_type_filter(client):
    assert envelope(post(client, "/pos/print/strategy/list", {"strategyType": 2}))
    assert envelope(post(client, "/pos/print/strategy/list", {"strategyType": 99})) == []
    assert envelope(post(client, "/pos/print/strategy/list", {"strategyType": "x"}))
    assert envelope(post(client, "/pos/print/strategy/list", {"strategyType": 0}))


def test_strategy_info_fields(client):
    d = envelope(post(client, "/pos/print/strategy/info", {}))
    assert set(d) == {"foods", "addFoodNum", "strategyType", "defaultStrategySwitch",
                      "deskIds", "orderTypeSwitch", "refundFoodNum",
                      "orderSourceSwitch", "addFoodSwitch", "strategyName",
                      "changeDeskSwitch", "refundFoodSwitch", "singleFoodSwitch"}
    assert isinstance(d["foods"], list)


def test_strategy_save_then_read_back(client):
    """写接口必须落库：kv_set 之后不 commit 会静默丢数据。"""
    payload = {"strategyName": "传菜单", "addFoodNum": 3, "strategyType": 2,
               "defaultStrategySwitch": 1, "deskIds": [-1, 2], "orderTypeSwitch": 0,
               "refundFoodNum": 2, "orderSourceSwitch": 1, "addFoodSwitch": 1,
               "changeDeskSwitch": 1, "refundFoodSwitch": 1, "singleFoodSwitch": 0,
               "foods": [{"productId": 1001, "productName": "宫保鸡丁"}]}
    saved = envelope(post(client, "/pos/print/strategy/save", payload))
    assert len(saved["strategyId"]) == 24

    info = envelope(post(client, "/pos/print/strategy/info", {}))
    assert info["strategyName"] == "传菜单"
    assert info["addFoodNum"] == 3
    assert info["deskIds"] == [-1, 2]
    assert info["orderTypeSwitch"] == 0
    assert info["foods"] == [{"productId": 1001, "productName": "宫保鸡丁"}]


def test_strategy_save_syncs_device_snapshot(client):
    envelope(post(client, "/pos/print/strategy/save",
                  {"strategyName": "档口二", "strategyType": 2}))
    dev = envelope(post(client, "/pos/print/sync/device", {}))
    assert dev["printStrategyList"][0]["strategyName"] == "档口二"


def test_strategy_save_keeps_list_only_fields(client):
    """strategyId / bilTypeList / deviceList 是 list 独有字段，save 后不能丢。"""
    envelope(post(client, "/pos/print/strategy/save",
                  {"strategyId": "a" * 24, "bilTypeList": [3, 4],
                   "deviceList": [], "strategyName": "留存测试"}))
    rows = envelope(post(client, "/pos/print/strategy/list", {}))
    assert rows[0]["strategyName"] == "留存测试"


def test_strategy_save_ignores_unknown_keys(client):
    envelope(post(client, "/pos/print/strategy/save", {"whatever": 1}))
    info = envelope(post(client, "/pos/print/strategy/info", {}))
    assert set(info) == {"foods", "addFoodNum", "strategyType", "defaultStrategySwitch",
                         "deskIds", "orderTypeSwitch", "refundFoodNum",
                         "orderSourceSwitch", "addFoodSwitch", "strategyName",
                         "changeDeskSwitch", "refundFoodSwitch", "singleFoodSwitch"}


# --------------------------------------------------------------- 功能开关

def test_ability_open(client):
    d = envelope(post(client, "/open/store/queryAbilityOpen", {}))
    assert d["openStatus"] == 1
    assert d["invoiceSwitch"] == 0
    assert isinstance(d["typeChannelList"], list)


def test_merchant_config(client):
    d = envelope(post(client, "/open/merchant/queryMerchantConfig", {}))
    codes = [x["functionCode"] for x in d["functionCodeInfoList"]]
    assert codes == ["PTWM", "SFTG"]
    for x in d["functionCodeInfoList"]:
        assert {"chargingMode", "funcUsage", "functionCode", "productSpuCode",
                "expirationTime", "funcResidualUsage", "posNo"} <= set(x)


def test_store_config_format1(client):
    b = post(client, "/store/getStoreConfig/", {}).json()
    assert b["code"] == 200 and isinstance(b["data"], dict)
    assert b["data"]["openCashboxControl"] == 0
    assert b["data"]["refundVerifyStatus"] == 0
