"""逸华餐饮收银 —— 打印 / 功能开关 / 商户配置（旧版兼容层）

覆盖任务卡 P0-6、P0-7 两组接口：

    POST /pos/print/sync/template        打印模板同步（四段列表）
    POST /pos/print/sync/device          打印设备 + 打印策略同步
    POST /pos/print/strategy/list        打印策略列表
    POST /pos/print/strategy/info        打印策略详情
    POST /pos/print/strategy/save        打印策略保存
    POST /open/print/device/brandList    打印机品牌列表
    POST /open/print/device/list         已绑定打印设备列表
    POST /open/store/queryAbilityOpen    门店功能开关
    POST /open/merchant/queryMerchantConfig  商户功能授权

响应一律使用「格式二」：ApiResponse + data 二次编码 JSON 字符串。
模板 / 策略 / 开关全部按 tenant_id + store_id 隔离，持久化在 dictionary_table，
首次访问写入默认值（kv_get_or_init），之后由 strategy/save 等写接口更新。
"""
from __future__ import annotations

import uuid
from typing import Any, Dict, List, Optional

from fastapi import APIRouter, Depends, Request
from sqlalchemy.orm import Session

from ..db import get_db
from ..legacy_core import (envelope, kv_get_or_init, kv_set, parse_payload,
                           resolve_scope, s)

router = APIRouter(tags=["legacy-print"])

# ---------------------------------------------------------------- 默认数据

DEFAULT_PRINT_STRATEGY: Dict[str, Any] = {
    "tagPrintNum": 0,
    "takeoutPrintNum": 0,
    "memberRechargePrintSwitch": 0,
    "modifierPrintMode": 0,
    "addFoodNum": 1,
    "strategyType": 2,
    "defaultStrategySwitch": 1,
    "deskIds": [-1],
    "orderTypeSwitch": 1,
    "refundPrintNum": 0,
    "refundFoodNum": 1,
    "customOrderNum": 0,
    "orderSourceSwitch": 1,
    "addFoodSwitch": 1,
    "preSettlementNum": 0,
    "strategyName": "后厨档口",
    "preSettlementSwitch": 0,
    "changeDeskSwitch": 1,
    "changeDeskNum": 1,
    "memberRechargePrintNum": 0,
    "strategyStatus": 0,
    "payPrintNum": 0,
    "refundFoodSwitch": 1,
    "customOrderSwitch": 0,
    "modifierFoods": [],
    "singleFoodSwitch": 0,
    "takeoutAfterSalesPrintNum": 0,
}

# strategy/list 只回一个精简子集
STRATEGY_LIST_FIELDS = ("defaultStrategySwitch", "strategyName", "deviceList",
                        "strategyId", "modifierPrintMode", "bilTypeList",
                        "strategyType")

# strategy/info 的字段集合（不含 strategyId，与抓包一致）
STRATEGY_INFO_FIELDS = ("foods", "addFoodNum", "strategyType",
                        "defaultStrategySwitch", "deskIds", "orderTypeSwitch",
                        "refundFoodNum", "orderSourceSwitch", "addFoodSwitch",
                        "strategyName", "changeDeskSwitch", "refundFoodSwitch",
                        "singleFoodSwitch")

# 打印机品牌（抓包实录）
PRINTER_BRANDS: List[Dict[str, Any]] = [
    {"brand_name": "芯烨", "brand_id": 11},
    {"brand_name": "佳博", "brand_id": 12},
    {"brand_name": "其他", "brand_id": 13},
    {"brand_name": "汉印", "brand_id": 19},
    {"brand_name": "大趋智能", "brand_id": 25},
]

# 收银机内置打印机（本机），策略里 deviceList 默认指向它
BUILTIN_DEVICE: Dict[str, Any] = {
    "deviceId": "LOCAL001",
    "deviceName": "收银机内置打印机",
    "deviceType": 1,
    "brandId": 11,
    "brandName": "芯烨",
    "deviceNo": "LOCAL001",
    "deviceIp": "",
    "paperWidth": 58,
    "status": 1,
    "dataStatus": 0,
}

# 门店功能开关（抓包实录）
DEFAULT_ABILITY: Dict[str, Any] = {
    "invoiceSwitch": 0,
    "typeChannelList": [],
    "openStatus": 1,
    "invoiceStatus": 0,
}

# 商户功能授权（抓包实录两条：营销套餐 PTWM / 收付通 SFTG）
DEFAULT_MERCHANT_CONFIG: Dict[str, Any] = {
    "functionCodeInfoList": [
        {"chargingMode": 2, "funcUsage": "0", "functionCode": "PTWM",
         "productSpuCode": "MKT009", "expirationTime": "2030-12-31 23:59:59",
         "funcResidualUsage": "0", "posNo": ""},
        {"chargingMode": 2, "funcUsage": "0", "functionCode": "SFTG",
         "productSpuCode": "MKT008", "expirationTime": "2030-12-31 23:59:59",
         "funcResidualUsage": "0", "posNo": ""},
    ]
}

# dictionary_table 键名
K_TEMPLATE = "print.template"
K_DEVICE = "print.device"
K_STRATEGY = "print.strategy"
K_ABILITY = "ability.open"
K_MERCHANT = "merchant.config"

RECEIPT_TEMPLATE_TYPES = (
    ("预结单", 1), ("结账单", 2), ("交班单", 3), ("退菜单", 4),
    ("充值单", 5), ("会员消费单", 6), ("反结账单", 7), ("点菜单", 8),
    ("外送单", 9), ("挂账单", 10),
)


async def _read(request: Request):
    """旧版客户端是 form / json 混合体，统一解析。"""
    try:
        body = await request.json()
    except Exception:
        body = None
    if not isinstance(body, dict):
        body = {}
    return parse_payload(body)


def _sid(scope) -> Dict[str, Any]:
    return {"storeId": int(scope.store_id), "tenantId": int(scope.tenant_id)}


def _strategy_id(scope) -> str:
    """每门店一个稳定的策略 ID（24 位十六进制，与抓包格式一致）。"""
    return uuid.uuid5(uuid.NAMESPACE_URL,
                      "yihua-print-%s-%s" % (scope.tenant_id, scope.store_id)).hex[:24]


# ---------------------------------------------------------------- 模板同步

def _default_templates(scope) -> Dict[str, Any]:
    sid = int(scope.store_id)
    tid = int(scope.tenant_id)

    tag_template = {
        "id": sid * 100 + 1, "storeId": sid, "tenantId": tid,
        "templateId": "TAG0001",
        "storeName": 1, "storeNameFont": 2,
        "deskNameSwitch": 1, "deskNameFont": 2,
        "food": 1, "foodFont": 2,
        "specification": 1, "specificationFont": 1,
        "remarkSwitch": 1, "remarkFont": 1,
        "phone": 0, "phoneFont": 1,
        "orderType": 1, "takeoutSwitch": 0,
        "arriveTimeSwitch": 0, "arriveTimeFont": 1,
        "foodDelivery": 0, "foodDeliveryFont": 1,
        "dataStatus": 0,
    }

    receipt_template_list = []
    config_list = []
    order_template_list = []
    for idx, (name, ttype) in enumerate(RECEIPT_TEMPLATE_TYPES, start=1):
        template_id = "RCP%04d" % idx
        receipt_template_list.append({
            "id": sid * 1000 + idx, "storeId": sid, "tenantId": tid,
            "templateId": template_id,
            "templateName": name,
            "templateType": ttype,
            "templateAffiliation": 1,
            "createTime": "2026-01-01 00:00:00",
            "updateTime": "2026-01-01 00:00:00",
        })
        config_list.append({
            "id": sid * 1000 + idx, "storeId": sid,
            "templateId": template_id, "templateType": ttype,
            "templateSubType": 0,
        })
        order_template_list.append({
            "id": sid * 1000 + idx, "templateId": template_id,
            "storeName": 1, "storeNameFont": 2,
            "deskNameSwitch": 1, "deskNameFont": 2,
            "welcome": 1, "welcomeFont": 2, "welcomeMessage": "欢迎光临",
            "food": 1, "foodFont": 2,
            "foodSort": 1, "foodSpace": 1, "foodTypeFont": 1,
            "unitSwitch": 1, "prodUnitAmtSwitch": 1,
            "specification": 1, "specificationFont": 1,
            "remark": 1, "remarkFont": 1,
            "totalAmountSwitch": 1, "totalAmountFont": 2,
            "subTotalSwitch": 1,
            "discountSwitch": 1, "discountFont": 1,
            "discountDetailSwitch": 0, "discountDetailFont": 1,
            "paidAmount": 1, "paidAmountFont": 2,
            "payTypeSwitch": 1, "payTypeFont": 1,
            "memberInfoSwitch": 0, "memberInfoFont": 1,
            "memberBalance": 0, "memberBalanceFont": 1,
            "memberPoints": 0, "memberPointsFont": 1,
            "phoneSwitch": 0, "phoneFont": 1,
            "customerSwitch": 0,
            "qrcode": 0, "payQrCodeSwitch": 0,
            "invoiceSwitch": 0,
            "mealCode": 0,
            "printGroup": 1, "printGroupFont": 1,
            "refundAmount": 0, "refundReason": 0,
            "realRefundAmountSwitch": 0, "realRefundAmountFont": 1,
            "orderType": 1, "takeOut": 0,
            "arriveTimeSwitch": 0, "arriveTimeFont": 1,
            "foodDelivery": 0, "foodDeliveryFont": 1,
            "dataStatus": 0,
            "createTime": "2026-01-01 00:00:00",
            "updateTime": "2026-01-01 00:00:00",
        })
    # 清理占位键，避免出现抓包里没有的字段
    for t in order_template_list:
        t.pop("deskNameFont2", None)

    return {
        "tagTemplateList": [tag_template],
        "receiptTemplateList": receipt_template_list,
        "storeTemplateConfigList": config_list,
        "receiptOrderTemplateList": order_template_list,
    }


def _default_device(scope) -> Dict[str, Any]:
    """抓包响应只有 printStrategyList / deviceList 两个键；
    printStrategyList[0] 的字段集合见 DEFAULT_PRINT_STRATEGY（不含 deviceList）。"""
    ids = _sid(scope)
    strategy = dict(DEFAULT_PRINT_STRATEGY)
    strategy["id"] = _strategy_id(scope)
    strategy.update(ids)
    return {
        "printStrategyList": [strategy],
        "deviceList": [dict(BUILTIN_DEVICE, **ids)],
    }


def _strategy_list(scope, device_list: List[Dict[str, Any]],
                   stored: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
    """strategy/list 与 strategy/save 必须是同一份数据，否则存完读不到。"""
    strategy = dict(DEFAULT_PRINT_STRATEGY)
    strategy.update(stored or {})
    strategy["strategyId"] = s(strategy.get("strategyId")) or _strategy_id(scope)
    strategy["deviceList"] = device_list
    strategy["bilTypeList"] = strategy.get("bilTypeList") or [3, 4, 13]
    return [{k: strategy.get(k, DEFAULT_PRINT_STRATEGY.get(k))
             for k in STRATEGY_LIST_FIELDS}]


# ---------------------------------------------------------------- 接口实现

@router.post("/pos/print/sync/template")
async def print_sync_template(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                          K_TEMPLATE, _default_templates(scope))
    return envelope(data)


@router.post("/pos/print/sync/device")
async def print_sync_device(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                          K_DEVICE, _default_device(scope))
    return envelope(data)


@router.post("/pos/print/strategy/list")
async def print_strategy_list(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    device = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                            K_DEVICE, _default_device(scope))
    strategy_type = payload.get("strategyType")
    stored = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                            K_STRATEGY, dict(DEFAULT_PRINT_STRATEGY))
    rows = _strategy_list(scope, device.get("deviceList") or [], stored)
    if strategy_type not in (None, "", 0):
        try:
            st = int(strategy_type)
        except (TypeError, ValueError):
            st = None
        if st is not None:
            rows = [r for r in rows if r.get("strategyType") == st]
    return envelope(rows)


@router.post("/pos/print/strategy/info")
async def print_strategy_info(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    stored = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                            K_STRATEGY, dict(DEFAULT_PRINT_STRATEGY))
    merged = dict(DEFAULT_PRINT_STRATEGY)
    merged.update(stored or {})
    data = {k: merged.get(k, DEFAULT_PRINT_STRATEGY.get(k))
            for k in STRATEGY_INFO_FIELDS}
    # foods 与 strategy/save 里的 modifierFoods 是同一份数据
    data["foods"] = merged.get("foods") or merged.get("modifierFoods") or []
    return envelope(data)


@router.post("/pos/print/strategy/save")
async def print_strategy_save(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    current = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                             K_STRATEGY, dict(DEFAULT_PRINT_STRATEGY))
    merged = dict(DEFAULT_PRINT_STRATEGY)
    merged.update(current or {})
    for k, v in payload.items():
        if k in DEFAULT_PRINT_STRATEGY or k in STRATEGY_INFO_FIELDS:
            merged[k] = v
    # 客户端存的是 foods，模板内部叫 modifierFoods，两边保持同一份数据
    if "foods" in payload and "modifierFoods" not in payload:
        merged["modifierFoods"] = payload["foods"]
    # strategyId / bilTypeList / deviceList 是 strategy/list 才有的字段，一并留存
    for k in ("strategyId", "bilTypeList", "deviceList"):
        if k in payload:
            merged[k] = payload[k]
    kv_set(db, scope.tenant_id, scope.store_id, K_STRATEGY, merged)

    sid = s(payload.get("strategyId") or payload.get("id")) or _strategy_id(scope)

    # 同步刷新 device 快照，保证 sync/device 与 strategy/save 一致
    device = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                            K_DEVICE, _default_device(scope))
    strategies = device.get("printStrategyList") or []
    if strategies:
        strategies[0] = {k: merged.get(k, DEFAULT_PRINT_STRATEGY.get(k))
                         for k in DEFAULT_PRINT_STRATEGY}
        strategies[0]["id"] = sid
        device["printStrategyList"] = strategies
        kv_set(db, scope.tenant_id, scope.store_id, K_DEVICE, device)

    db.commit()
    return envelope({"strategyId": sid})


@router.post("/open/print/device/brandList")
async def print_brand_list(request: Request):
    await request.body()
    return envelope(PRINTER_BRANDS)


@router.post("/open/print/device/list")
async def print_device_list(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    device = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                            K_DEVICE, _default_device(scope))
    return envelope(device.get("deviceList") or [])


# ------------------------------------------------------- 功能开关 / 商户配置

@router.post("/open/store/queryAbilityOpen")
async def query_ability_open(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                          K_ABILITY, dict(DEFAULT_ABILITY))
    return envelope(data)


@router.post("/open/merchant/queryMerchantConfig")
async def query_merchant_config(request: Request, db: Session = Depends(get_db)):
    payload, meta = await _read(request)
    scope = resolve_scope(db, payload, meta, request)
    data = kv_get_or_init(db, scope.tenant_id, scope.store_id,
                          K_MERCHANT, dict(DEFAULT_MERCHANT_CONFIG))
    return envelope(data)
