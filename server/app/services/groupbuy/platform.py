"""PlatformAdapter 抽象基类 + 适配器注册表。

**架构约束（任务卡 4.6，硬要求）**：业务路由只依赖 ``PlatformAdapter``，
代码里不出现 ``if platform == "meituan"`` 这类分支。加一个新平台 =
新增一个 adapter 文件 —— 业务代码零改动，这一点由
``tests/test_groupbuy_platform.py`` 用测试证明。

适配器通过 :func:`register_adapter` 自注册，注册表在首次使用时扫描本包
下所有模块（``pkgutil``），所以新增文件即被自动发现、无需改动任何注册清单。
"""
import importlib
import pkgutil
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from decimal import Decimal
from typing import Dict, List, Optional, Type

from .errors import (CouponErrorCode, GroupbuyError, PlatformCode,
                     PlatformErrorCode)

ZERO = Decimal("0.00")


# ==================== 请求 / 结果 DTO ====================
@dataclass
class CouponQuery:
    """一次验券/核销请求的全部上下文（adapter 只认这个对象）。

    ``sign_key`` 是解出来的**明文**密钥，adapter 内部用完即弃，
    **任何情况下都不许写进日志/异常消息**（用 ``mask_secret`` 脱敏）。
    """
    platform_code: str
    coupon_code: str
    store_id: int
    tenant_id: int
    sign_key: str = ""
    app_id: str = ""
    order_id: int = 0
    actual_discount: Optional[Decimal] = None
    operator_id: int = 0
    booked: bool = False                      # 是否已完成预约（COUPON_NEED_BOOKING 用）
    reason: str = ""                          # 撤销核销的原因
    biz_time: Optional[datetime] = None       # 业务时间，测试可注入以覆盖时段限制


@dataclass
class CouponInfo:
    coupon_code: str
    platform_code: str
    platform_coupon_id: str = ""
    coupon_name: str = ""
    coupon_type: str = "cash"                 # package 套餐券 | cash 代金券
    face_value: Decimal = ZERO
    discount_amount: Decimal = ZERO
    valid_from: Optional[datetime] = None
    valid_to: Optional[datetime] = None
    restrictions: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        """对外 JSON 形态：金额一律走 str，避免 float 精度丢失。"""
        return {
            "couponCode": self.coupon_code,
            "platformCode": self.platform_code,
            "platformCouponId": self.platform_coupon_id,
            "couponName": self.coupon_name,
            "couponType": self.coupon_type,
            "faceValue": str(self.face_value),
            "discountAmount": str(self.discount_amount),
            "validFrom": self.valid_from.strftime("%Y-%m-%d %H:%M:%S") if self.valid_from else "",
            "validTo": self.valid_to.strftime("%Y-%m-%d %H:%M:%S") if self.valid_to else "",
            "restrictions": self.restrictions or {},
        }


@dataclass
class VerifyResult:
    valid: bool
    coupon: Optional[CouponInfo] = None
    error_code: Optional[str] = None
    message: str = ""

    def to_dict(self) -> dict:
        d = {"valid": self.valid,
             "coupon": self.coupon.to_dict() if self.coupon else None}
        if not self.valid:
            # 细分错误码 + 可直接展示的中文，客户端不需要自己维护文案表
            d["errorCode"] = self.error_code
            d["message"] = self.message
        return d


@dataclass
class RedeemResult:
    success: bool
    coupon: Optional[CouponInfo] = None
    error_code: Optional[str] = None
    message: str = ""
    redeem_id: Optional[int] = None
    actual_discount: Decimal = ZERO

    def __bool__(self) -> bool:
        """让 ``if adapter.reverse_redeem(q):`` 这种写法也能用（任务卡里的 bool 语义）。"""
        return bool(self.success)

    def to_dict(self) -> dict:
        d = {"success": self.success,
             "coupon": self.coupon.to_dict() if self.coupon else None,
             "redeemId": self.redeem_id,
             "actualDiscount": str(self.actual_discount)}
        if not self.success:
            d["errorCode"] = self.error_code
            d["message"] = self.message
        return d


@dataclass
class PlatformRedeemRecord:
    """平台侧的核销记录（对账时与本地记录逐条比对）。"""
    platform_code: str
    coupon_code: str
    redeem_time: Optional[datetime] = None
    discount_amount: Decimal = ZERO
    order_no: str = ""

    def to_dict(self) -> dict:
        return {"platformCode": self.platform_code, "couponCode": self.coupon_code,
                "redeemTime": self.redeem_time.strftime("%Y-%m-%d %H:%M:%S")
                              if self.redeem_time else "",
                "discountAmount": str(self.discount_amount), "orderNo": self.order_no}


# ==================== 抽象基类 ====================
class PlatformAdapter(ABC):
    """平台对接适配器。业务代码只依赖这个接口。"""

    #: 该适配器负责的平台编码（PlatformCode 的 value）
    platform_code: str = ""

    @abstractmethod
    def verify_coupon(self, req: CouponQuery) -> VerifyResult:
        """验券。**不修改任何状态**，可以被反复调用。"""

    @abstractmethod
    def redeem_coupon(self, req: CouponQuery) -> RedeemResult:
        """核销。必须幂等：同一张券只能成功一次。"""

    @abstractmethod
    def reverse_redeem(self, req: CouponQuery) -> RedeemResult:
        """撤销核销（误操作）。返回对象的布尔值为真即表示撤销成功。"""

    @abstractmethod
    def reconcile(self, req: CouponQuery, day: str) -> List[PlatformRedeemRecord]:
        """拉取平台侧某一天的核销记录，用于对账。"""

    @abstractmethod
    def verify_sign(self, payload: dict, sign: str) -> bool:
        """校验平台回调签名。"""


class SkeletonAdapter(PlatformAdapter):
    """未接入平台的骨架实现。

    骨架**不返回成功**、也不做任何模拟 —— 统一回 ``PLATFORM_NOT_INTEGRATED``，
    让「平台没接」这件事在收银端是可见的，而不是静默地放行一张来路不明的券。
    """

    def _not_integrated(self):
        return GroupbuyError(PlatformErrorCode.PLATFORM_NOT_INTEGRATED,
                             "平台未接入：%s" % self.platform_code)

    def verify_coupon(self, req: CouponQuery) -> VerifyResult:
        raise self._not_integrated()

    def redeem_coupon(self, req: CouponQuery) -> RedeemResult:
        raise self._not_integrated()

    def reverse_redeem(self, req: CouponQuery) -> RedeemResult:
        raise self._not_integrated()

    def reconcile(self, req: CouponQuery, day: str) -> List[PlatformRedeemRecord]:
        raise self._not_integrated()

    def verify_sign(self, payload: dict, sign: str) -> bool:
        raise self._not_integrated()


# ==================== 注册表 ====================
_ADAPTERS: Dict[str, Type[PlatformAdapter]] = {}


#: 包内不属于适配器的模块（基础设施），扫描时跳过
_INFRA_MODULES = frozenset(("errors", "crypto", "guard", "store", "verify",
                            "reconciliation", "platform"))


def register_adapter(cls: Type[PlatformAdapter]) -> Type[PlatformAdapter]:
    """把适配器注册进注册表（用作类装饰器）。

    新增平台只需要在自己的模块里写 ``@register_adapter class Xxx(...)``，
    不需要修改本文件、更不需要改业务路由。
    """
    code = str(getattr(cls, "platform_code", "") or "").strip().lower()
    if not code:
        raise ValueError("适配器缺少 platform_code: %s" % cls)
    _ADAPTERS[code] = cls
    return cls


def _register_from_module(mod) -> None:
    """把一个已加载模块里的适配器类补登记进注册表。

    装饰器只在模块**首次执行**时触发；``reload_adapters()`` 清表之后，
    已经 import 过的适配器模块不会再执行一遍，所以这里按类属性重扫一遍。
    """
    for obj in list(vars(mod).values()):
        if not isinstance(obj, type) or not issubclass(obj, PlatformAdapter):
            continue
        if obj in (PlatformAdapter, SkeletonAdapter):
            continue
        code = str(getattr(obj, "platform_code", "") or "").strip().lower()
        if code:
            _ADAPTERS[code] = obj


def load_adapters() -> None:
    """扫描本包下的所有模块，触发它们自注册。幂等、可重复调用。

    循环导入注意：包内模块（如 self_platform）会 import 本模块 ——
    正在初始化中的模块先跳过，等它自己的 ``@register_adapter`` 执行完自然注册。
    """
    import os as _os
    import sys as _sys
    package = __package__
    pkg_dir = _os.path.dirname(_os.path.abspath(__file__))
    for mod in pkgutil.iter_modules([pkg_dir]):
        name = mod.name
        if name in _INFRA_MODULES:
            continue
        full = "%s.%s" % (package, name)
        loaded = _sys.modules.get(full)
        if loaded is not None:
            spec = getattr(loaded, "__spec__", None)
            if spec is not None and getattr(spec, "_initializing", False):
                continue          # 正在导入本模块（循环导入），它自己会注册
            _register_from_module(loaded)
            continue
        _register_from_module(importlib.import_module(full))


def get_adapter(platform_code: str) -> PlatformAdapter:
    """按平台编码取适配器实例。

    未知平台抛 ``PLATFORM_UNKNOWN``，未接入平台抛 ``PLATFORM_NOT_INTEGRATED`` ——
    这两种情况客户端要给的提示完全不同，不能混成一个错误。
    """
    load_adapters()
    code = (platform_code or "").strip().lower()
    if not code:
        raise GroupbuyError(PlatformErrorCode.PLATFORM_UNKNOWN, "platformCode 不能为空")
    cls = _ADAPTERS.get(code)
    if cls is None:
        known = ", ".join(sorted(_ADAPTERS))
        raise GroupbuyError(PlatformErrorCode.PLATFORM_UNKNOWN,
                            "未知平台 %s（已支持：%s）" % (code, known))
    return cls()


def registered_platforms() -> List[str]:
    load_adapters()
    return sorted(_ADAPTERS)


def reload_adapters() -> None:
    """清空注册表并重新扫描本包（新写入的 adapter 文件也能被发现）。

    给测试用：证明「加新平台 = 加一个 adapter 文件」而不用重启进程。
    """
    _ADAPTERS.clear()
    importlib.invalidate_caches()
    load_adapters()


def is_known_platform(platform_code: str) -> bool:
    load_adapters()
    return (platform_code or "").strip().lower() in _ADAPTERS
