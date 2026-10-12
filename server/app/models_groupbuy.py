"""团购域数据模型 —— 独立文件，不污染现有 models.py。

对应任务卡 T-405：
  * 平台配置（多平台，密钥密文存储）
  * 团购券（验券 / 核销 / 撤销）
  * 核销流水（谁、什么时候、哪个订单、抵了多少 —— 对账与报表的数据源）
"""
from datetime import datetime

from sqlalchemy import (BigInteger, Column, DateTime, Index, Integer, JSON,
                        Numeric, SmallInteger, String, Text, UniqueConstraint)

from .db import Base

MONEY = Numeric(12, 2)

# SQLite 只有 INTEGER PRIMARY KEY 才能自增，MySQL 侧仍用 BIGINT（与 models.py 一致）
BigIntPK = BigInteger().with_variant(Integer, "sqlite")

# ---------------- 券状态 ----------------
COUPON_UNUSED = 1    # 待核销（撤销核销后回到这个状态，可重新核销）
COUPON_USED = 2      # 已核销（锁定，不能重复用）


# ==================== 平台配置 ====================
class GroupbuyPlatformConfig(Base):
    __tablename__ = "groupbuy_platform_config"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    platform_code = Column(String(16), nullable=False)     # meituan | eleme | douyin | wechat | self
    store_ids_json = Column(JSON, default=list)            # 一个平台可绑多个门店
    app_id = Column(String(64), default="")
    sign_key_cipher = Column(Text, default="")             # 密钥密文（不落明文，见 services/groupbuy/crypto.py）
    status = Column(SmallInteger, default=1)               # 1启用 0停用
    remark = Column(String(255), default="")
    created_at = Column(DateTime, default=datetime.now)
    updated_at = Column(DateTime, default=datetime.now, onupdate=datetime.now)
    __table_args__ = (UniqueConstraint("tenant_id", "platform_code",
                                       name="uk_gb_tenant_platform"),)


# ==================== 团购券 ====================
class GroupbuyCoupon(Base):
    __tablename__ = "groupbuy_coupon"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False)
    platform_code = Column(String(16), nullable=False)
    coupon_code = Column(String(64), nullable=False)       # 客人出示的券码
    platform_coupon_id = Column(String(64), default="")    # 平台侧券模板 ID
    coupon_name = Column(String(128), default="")
    coupon_type = Column(String(16), default="cash")       # package 套餐券 | cash 代金券
    face_value = Column(MONEY, default=0)                  # 面额
    discount_amount = Column(MONEY, default=0)             # 抵扣金额
    valid_from = Column(DateTime, nullable=True)
    valid_to = Column(DateTime, nullable=True)
    restrictions = Column(JSON, default=dict)              # 使用限制（门店/时段/预约）
    status = Column(SmallInteger, default=COUPON_UNUSED)
    store_id = Column(BigInteger, default=0)               # 核销门店
    order_id = Column(BigInteger, default=0)               # 核销关联的本地订单
    actual_discount = Column(MONEY, default=0)             # 实际抵扣（商家让利后）
    redeemed_at = Column(DateTime, nullable=True)
    redeemed_by = Column(BigInteger, default=0)
    reversed_at = Column(DateTime, nullable=True)
    reversed_by = Column(BigInteger, default=0)
    created_at = Column(DateTime, default=datetime.now)
    __table_args__ = (
        UniqueConstraint("tenant_id", "platform_code", "coupon_code",
                         name="uk_gb_coupon_code"),
        Index("idx_gb_coupon_store_time", "store_id", "redeemed_at"),
    )


# ==================== 核销流水 ====================
class GroupbuyRedeemLog(Base):
    __tablename__ = "groupbuy_redeem_log"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False)
    store_id = Column(BigInteger, nullable=False, index=True)
    coupon_id = Column(BigInteger, nullable=False)
    coupon_code = Column(String(64), nullable=False)
    platform_code = Column(String(16), nullable=False)
    order_id = Column(BigInteger, default=0)
    actual_discount = Column(MONEY, default=0)
    action = Column(String(16), nullable=False)            # redeem 核销 | reverse 撤销
    reason = Column(String(255), default="")               # 撤销原因
    operator_id = Column(BigInteger, default=0)
    created_at = Column(DateTime, default=datetime.now)
    __table_args__ = (Index("idx_gb_log_store_time", "store_id", "created_at"),)
