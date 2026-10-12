"""数据模型（对应 docs/02-API契约与数据模型.md 第 4 节 DDL）。"""
from datetime import datetime

from sqlalchemy import (BigInteger, Column, DateTime, Integer, JSON, Numeric,
                        SmallInteger, String, Text, UniqueConstraint, Index)

from .db import Base

MONEY = Numeric(12, 2)
QTY = Numeric(12, 3)

# SQLite 只有 INTEGER PRIMARY KEY 才能自增，MySQL 侧仍用 BIGINT
BigIntPK = BigInteger().with_variant(Integer, "sqlite")


# ==================== 租户与组织 ====================
class Tenant(Base):
    __tablename__ = "tenant"
    id = Column(BigIntPK, primary_key=True)
    code = Column(String(32), unique=True, nullable=False)
    name = Column(String(64), nullable=False)
    edition = Column(String(16), default="STD")
    status = Column(SmallInteger, default=1)
    expire_at = Column(DateTime, nullable=True)
    created_at = Column(DateTime, default=datetime.now)


class Store(Base):
    __tablename__ = "store"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    code = Column(String(32), nullable=False)
    name = Column(String(64), nullable=False)
    store_mode = Column(SmallInteger, default=1)      # 1堂食 2快餐 3外卖
    address = Column(String(255))
    phone = Column(String(20))
    status = Column(SmallInteger, default=1)
    __table_args__ = (UniqueConstraint("tenant_id", "code", name="uk_tenant_code"),)


class Terminal(Base):
    __tablename__ = "terminal"
    id = Column(BigIntPK, primary_key=True)
    store_id = Column(BigInteger, nullable=False, index=True)
    no = Column(String(16), nullable=False)
    name = Column(String(32))
    device_id = Column(String(64))
    last_online_at = Column(DateTime, nullable=True)
    status = Column(SmallInteger, default=1)
    __table_args__ = (UniqueConstraint("store_id", "no", name="uk_store_no"),)


class SysRole(Base):
    __tablename__ = "sys_role"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    name = Column(String(32), nullable=False)
    permissions_json = Column(JSON, default=list)


class SysUser(Base):
    __tablename__ = "sys_user"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False)
    store_id = Column(BigInteger, default=0)
    account = Column(String(32), nullable=False)
    pwd_hash = Column(String(128), nullable=False)
    name = Column(String(32))
    role_id = Column(BigInteger)
    status = Column(SmallInteger, default=1)
    __table_args__ = (UniqueConstraint("tenant_id", "account", name="uk_tenant_account"),)


# ==================== 商品 ====================
class ProductCategory(Base):
    __tablename__ = "product_category"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    name = Column(String(64), nullable=False)
    pc_type = Column(SmallInteger, default=1)   # 1基础分类 0营销分类
    sort = Column(Integer, default=0)
    data_status = Column(SmallInteger, default=1)
    version = Column(BigInteger, default=1)


class ProductUnit(Base):
    __tablename__ = "product_unit"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    name = Column(String(16), nullable=False)
    sort = Column(Integer, default=0)
    version = Column(BigInteger, default=1)


class Product(Base):
    __tablename__ = "product"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False, index=True)
    name = Column(String(64), nullable=False)
    code = Column(String(32))
    barcode = Column(String(32))
    category_id = Column(BigInteger)
    unit_id = Column(BigInteger)
    unit_name = Column(String(16))
    price = Column(MONEY, default=0)
    member_price = Column(MONEY, default=0)
    cost = Column(MONEY, default=0)
    spec_group = Column(String(64))
    product_type = Column(SmallInteger, default=1)   # 1普通 2套餐 3称重
    weight_way = Column(SmallInteger, default=0)
    status = Column(SmallInteger, default=1)
    sort = Column(Integer, default=0)
    pre_pin_yin = Column(String(32))
    version = Column(BigInteger, default=1)
    __table_args__ = (
        Index("idx_tenant_cat", "tenant_id", "category_id"),
        Index("idx_barcode", "tenant_id", "barcode"),
    )


# ==================== 桌台 ====================
class DeskArea(Base):
    __tablename__ = "desk_area"
    id = Column(BigIntPK, primary_key=True)
    store_id = Column(BigInteger, nullable=False, index=True)
    name = Column(String(32))
    sort = Column(Integer, default=0)


class Desk(Base):
    __tablename__ = "desk"
    id = Column(BigIntPK, primary_key=True)
    store_id = Column(BigInteger, nullable=False)
    area_id = Column(BigInteger)
    name = Column(String(32))
    capacity = Column(Integer, default=4)
    status = Column(SmallInteger, default=0)   # 0空闲 1占用 2预订
    current_order_id = Column(BigInteger, default=0)
    __table_args__ = (Index("idx_store_area", "store_id", "area_id"),)


# ==================== 订单 ====================
class Order(Base):
    __tablename__ = "t_order"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False)
    store_id = Column(BigInteger, nullable=False)
    order_no = Column(String(32), nullable=False)
    client_order_no = Column(String(64), nullable=False)
    desk_id = Column(BigInteger, default=0)
    order_type = Column(SmallInteger, default=1)   # 1堂食 2快餐 3外卖 4自提
    source = Column(SmallInteger, default=1)       # 1POS 2安卓 3小程序
    status = Column(SmallInteger, default=0)       # 0待结 1已结 2已退 3作废
    member_id = Column(BigInteger, default=0)
    amount_total = Column(MONEY, default=0)
    amount_discount = Column(MONEY, default=0)
    order_discount = Column(MONEY, default=0)      # 整单折扣部分（单品折扣另计）
    amount_pay = Column(MONEY, default=0)
    amount_refund = Column(MONEY, default=0)
    settle_time = Column(DateTime, nullable=True)
    shift_id = Column(BigInteger, default=0)
    operator_id = Column(BigInteger)
    remark = Column(String(255))
    created_at = Column(DateTime, default=datetime.now)
    __table_args__ = (
        UniqueConstraint("client_order_no", name="uk_client_no"),
        UniqueConstraint("tenant_id", "order_no", name="uk_order_no"),
        Index("idx_store_time", "store_id", "created_at"),
        Index("idx_status", "store_id", "status"),
    )


class OrderItem(Base):
    __tablename__ = "t_order_item"
    id = Column(BigIntPK, primary_key=True)
    order_id = Column(BigInteger, nullable=False, index=True)
    product_id = Column(BigInteger)
    product_name = Column(String(64))
    spec_text = Column(String(128))
    price = Column(MONEY, default=0)
    qty = Column(QTY, default=1)
    amount = Column(MONEY, default=0)
    discount_amount = Column(MONEY, default=0)
    gift_flag = Column(SmallInteger, default=0)
    remark = Column(String(128))
    status = Column(SmallInteger, default=0)   # 0正常 1已退


class OrderFlow(Base):
    __tablename__ = "t_order_flow"
    id = Column(BigIntPK, primary_key=True)
    order_id = Column(BigInteger, nullable=False, index=True)
    action = Column(String(32))
    from_status = Column(SmallInteger)
    to_status = Column(SmallInteger)
    operator_id = Column(BigInteger)
    created_at = Column(DateTime, default=datetime.now)


# ==================== 支付 ====================
class Payment(Base):
    __tablename__ = "payment"
    id = Column(BigIntPK, primary_key=True)
    order_id = Column(BigInteger, nullable=False, index=True)
    pay_type = Column(String(16), nullable=False)
    channel = Column(String(16))
    amount = Column(MONEY, nullable=False)
    status = Column(SmallInteger, default=0)   # 0待付 1成功 2失败 3已退
    out_trade_no = Column(String(64), unique=True)
    channel_trade_no = Column(String(64))
    pay_time = Column(DateTime, nullable=True)
    operator_id = Column(BigInteger)


# ==================== 会员 ====================
class Member(Base):
    __tablename__ = "member"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, nullable=False)
    phone = Column(String(20), nullable=False)
    name = Column(String(32))
    level_id = Column(BigInteger, default=1)
    balance = Column(MONEY, default=0)
    score = Column(Integer, default=0)
    status = Column(SmallInteger, default=1)
    __table_args__ = (UniqueConstraint("tenant_id", "phone", name="uk_tenant_phone"),)


class MemberBalanceLog(Base):
    __tablename__ = "member_balance_log"
    id = Column(BigIntPK, primary_key=True)
    member_id = Column(BigInteger, nullable=False, index=True)
    type = Column(SmallInteger)   # 1充值 2消费 3退款 4调整
    amount = Column(MONEY)
    balance_before = Column(MONEY)
    balance_after = Column(MONEY)
    order_id = Column(BigInteger, default=0)
    operator_id = Column(BigInteger)
    created_at = Column(DateTime, default=datetime.now)


# ==================== 交班 ====================
class Shift(Base):
    __tablename__ = "shift"
    id = Column(BigIntPK, primary_key=True)
    store_id = Column(BigInteger, nullable=False, index=True)
    terminal_no = Column(String(16))
    operator_id = Column(BigInteger)
    start_at = Column(DateTime, default=datetime.now)
    end_at = Column(DateTime, nullable=True)
    cash_open = Column(MONEY, default=0)
    cash_close = Column(MONEY, default=0)
    cash_should = Column(MONEY, default=0)
    cash_actual = Column(MONEY, default=0)
    status = Column(SmallInteger, default=0)   # 0进行中 1已交班


# ==================== 通用字典 ====================
class Dictionary(Base):
    __tablename__ = "dictionary_table"
    id = Column(BigIntPK, primary_key=True)
    tenant_id = Column(BigInteger, default=0)
    store_id = Column(BigInteger, default=0)
    key = Column(String(64), nullable=False)
    value = Column(Text)
    __table_args__ = (UniqueConstraint("tenant_id", "store_id", "key", name="uk_scope_key"),)
