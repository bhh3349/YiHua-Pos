"""平台配置与上下文构建（业务路由与 adapter 之间的粘合层）。

职责：
  * 平台配置 CRUD（密钥密文存取，读出时才解密、不落日志）
  * 构建 :class:`CouponQuery`（解出 sign_key 塞给 adapter —— adapter 内用完即弃）
  * 平台启用状态检查
"""
from datetime import datetime
from decimal import Decimal, InvalidOperation
from typing import List, Optional

from sqlalchemy.orm import Session

from ...models_groupbuy import GroupbuyPlatformConfig
from .crypto import decrypt_secret, encrypt_secret, mask_secret
from .errors import (GroupbuyError, PlatformCode, PlatformErrorCode,
                     RequestErrorCode)
from .platform import CouponQuery, get_adapter, is_known_platform


# ---------------- 配置 CRUD ----------------
def list_configs(db: Session, tenant_id: int) -> List[dict]:
    rows = (db.query(GroupbuyPlatformConfig)
              .filter(GroupbuyPlatformConfig.tenant_id == tenant_id)
              .order_by(GroupbuyPlatformConfig.id).all())
    return [config_dto(r) for r in rows]


def config_dto(r: GroupbuyPlatformConfig) -> dict:
    """配置的对外形态。**sign_key 永不出现在这里** —— 只回掩码，
    客户端界面用掩码显示「已配置」，要换密钥就整条重新提交。"""
    return {
        "id": r.id,
        "platformCode": r.platform_code,
        "storeIdList": r.store_ids_json or [],
        "appId": r.app_id or "",
        "signKeyMasked": mask_secret(decrypt_secret(r.sign_key_cipher or "")),
        "hasSignKey": bool(r.sign_key_cipher),
        "status": r.status,
        "remark": r.remark or "",
        "updatedAt": r.updated_at.strftime("%Y-%m-%d %H:%M:%S") if r.updated_at else "",
    }


def upsert_config(db: Session, tenant_id: int, platform_code: str,
                  store_ids: List[int], sign_key: str, app_id: str = "",
                  remark: str = "", status: int = 1) -> GroupbuyPlatformConfig:
    """新增/更新平台配置。密钥只进不出：入库前加密，返回模型本身（不带明文）。"""
    if not is_known_platform(platform_code):
        raise GroupbuyError(PlatformErrorCode.PLATFORM_UNKNOWN)
    row = (db.query(GroupbuyPlatformConfig)
             .filter(GroupbuyPlatformConfig.tenant_id == tenant_id,
                     GroupbuyPlatformConfig.platform_code == platform_code)
             .first())
    if row is None:
        row = GroupbuyPlatformConfig(tenant_id=tenant_id, platform_code=platform_code)
        db.add(row)
    row.store_ids_json = [int(x) for x in (store_ids or [])]
    row.app_id = (app_id or "").strip()
    row.remark = (remark or "")[:255]
    row.status = 1 if status else 0
    if sign_key:                       # 不传表示不修改密钥
        row.sign_key_cipher = encrypt_secret(sign_key)
    db.commit()
    return row


def set_config_status(db: Session, tenant_id: int, platform_code: str,
                      status: int) -> GroupbuyPlatformConfig:
    row = _get_config(db, tenant_id, platform_code)
    row.status = 1 if status else 0
    db.commit()
    return row


def _get_config(db: Session, tenant_id: int, platform_code: str) -> GroupbuyPlatformConfig:
    row = (db.query(GroupbuyPlatformConfig)
             .filter(GroupbuyPlatformConfig.tenant_id == tenant_id,
                     GroupbuyPlatformConfig.platform_code == platform_code)
             .first())
    if row is None:
        raise GroupbuyError(RequestErrorCode.CONFIG_NOT_FOUND)
    return row


# ---------------- 业务校验 ----------------
def ensure_platform_enabled(db: Session, tenant_id: int, platform_code: str) -> GroupbuyPlatformConfig:
    """平台必须「认识 + 已配置 + 启用」才能验券/核销。

    先判「认不认识」再查配置：平台编码写错（PLATFORM_UNKNOWN）和平台没配
    （CONFIG_NOT_FOUND）对客户端是两件事，不能混成一个错误。
    """
    if not is_known_platform(platform_code):
        raise GroupbuyError(PlatformErrorCode.PLATFORM_UNKNOWN,
                            "未知平台 %s" % platform_code)
    cfg = _get_config(db, tenant_id, platform_code)
    if not cfg.status:
        raise GroupbuyError(PlatformErrorCode.PLATFORM_DISABLED)
    return cfg


def check_store_allowed(cfg: GroupbuyPlatformConfig, store_id: int) -> None:
    """门店必须在平台绑定名单内（一个平台可绑多门店）。"""
    allowed = [int(x) for x in (cfg.store_ids_json or [])]
    if allowed and store_id not in allowed:
        raise GroupbuyError("COUPON_STORE_MISMATCH", "当前门店未绑定该平台")


# ---------------- 请求构建 ----------------
def parse_decimal(value, field: str) -> Optional[Decimal]:
    """入参金额转 Decimal。字符串/整数/浮点都收，但**内部一律 Decimal**。

    用 ``str(value)`` 而不是 ``Decimal(value)`` 直接构造：float 进来时先
    转字符串再转 Decimal，避免 ``Decimal(0.1)`` 那种二进制尾巴。
    """
    if value is None or value == "":
        return None
    try:
        d = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise GroupbuyError(RequestErrorCode.INVALID_PARAM, "%s 不是合法金额" % field)
    return d


def build_query(db: Session, tenant_id: int, store_id: int, platform_code: str,
                coupon_code: str, order_id: int = 0, actual_discount=None,
                operator_id: int = 0, booked: bool = False, reason: str = "",
                biz_time: Optional[datetime] = None) -> CouponQuery:
    """构建 adapter 入参。sign_key 在这里解密、塞进 Query，
    除此之外任何地方都不再持有明文（adapter 用完即弃）。"""
    cfg = _get_config(db, tenant_id, platform_code)
    return CouponQuery(
        platform_code=platform_code, coupon_code=(coupon_code or "").strip(),
        store_id=store_id, tenant_id=tenant_id,
        sign_key=decrypt_secret(cfg.sign_key_cipher or ""),
        app_id=cfg.app_id or "",
        order_id=order_id or 0,
        actual_discount=parse_decimal(actual_discount, "actualDiscount"),
        operator_id=operator_id or 0,
        booked=bool(booked), reason=reason or "", biz_time=biz_time,
    )


def adapter_for(platform_code: str):
    return get_adapter(platform_code)
