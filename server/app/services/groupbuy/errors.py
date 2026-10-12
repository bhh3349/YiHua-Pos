"""团购错误码与平台常量。

设计要点（任务卡 4.2）：验券失败**必须细分**，不能笼统回「券无效」——
收银员看到「已过期」和看到「本店不可用」要做的事完全不同。

错误码分两层：
  * ``CouponErrorCode``  —— 券本身的 8 种状态（验券/核销共用，可对外暴露给客户端做 UI 提示）
  * ``PlatformErrorCode`` —— 平台接入层错误（未接入 / 验签失败 / 限流）
"""
from enum import Enum


class PlatformCode(str, Enum):
    """平台编码用枚举，业务代码里不散落字符串字面量。

    新增平台 = 加一个枚举值 + 一个 adapter 文件（见 platform.py 注册表），
    业务路由代码不需要改动。
    """
    MEITUAN = "meituan"
    ELEME = "eleme"
    DOUYIN = "douyin"
    WECHAT = "wechat"
    SELF = "self"

    def __str__(self) -> str:      # 让 f-string / 日志里直接出 "meituan" 而不是 "PlatformCode.MEITUAN"
        return self.value


# 平台中文名：仅用于人读的提示与报表，放这里集中维护
PLATFORM_NAMES = {
    PlatformCode.MEITUAN: "美团",
    PlatformCode.ELEME: "饿了么",
    PlatformCode.DOUYIN: "抖音",
    PlatformCode.WECHAT: "微信",
    PlatformCode.SELF: "自有小程序",
}


class CouponErrorCode(str, Enum):
    """券状态错误码 —— 8 种，一一对应任务卡的校验表。"""
    COUPON_NOT_FOUND = "COUPON_NOT_FOUND"            # 券码不存在
    COUPON_USED = "COUPON_USED"                      # 已核销
    COUPON_NOT_YET_VALID = "COUPON_NOT_YET_VALID"    # 未到有效期
    COUPON_EXPIRED = "COUPON_EXPIRED"                # 已过期
    COUPON_STORE_MISMATCH = "COUPON_STORE_MISMATCH"  # 门店不匹配
    COUPON_TIME_RESTRICTED = "COUPON_TIME_RESTRICTED"  # 时段不符
    COUPON_NEED_BOOKING = "COUPON_NEED_BOOKING"      # 需要预约
    PLATFORM_MISMATCH = "PLATFORM_MISMATCH"          # 平台不匹配

    def __str__(self) -> str:
        return self.value


# 券状态 -> 中文提示。收银端可直接展示，不需要自己维护一份文案。
COUPON_ERROR_MESSAGES = {
    CouponErrorCode.COUPON_NOT_FOUND: "券码不存在，请核对后重新输入",
    CouponErrorCode.COUPON_USED: "该券已核销，不能重复使用",
    CouponErrorCode.COUPON_NOT_YET_VALID: "该券尚未生效，未到使用开始时间",
    CouponErrorCode.COUPON_EXPIRED: "该券已过期",
    CouponErrorCode.COUPON_STORE_MISMATCH: "该券不适用于当前门店",
    CouponErrorCode.COUPON_TIME_RESTRICTED: "当前时段不可使用该券（如仅限午市）",
    CouponErrorCode.COUPON_NEED_BOOKING: "该券需提前预约，请先完成预约",
    CouponErrorCode.PLATFORM_MISMATCH: "券码与所选平台不匹配",
}


class PlatformErrorCode(str, Enum):
    """平台接入层错误。"""
    PLATFORM_NOT_INTEGRATED = "PLATFORM_NOT_INTEGRATED"  # 平台未接入（骨架 adapter 返回）
    PLATFORM_UNKNOWN = "PLATFORM_UNKNOWN"                # 平台编码不认识
    PLATFORM_DISABLED = "PLATFORM_DISABLED"              # 平台配置被停用
    SIGN_INVALID = "SIGN_INVALID"                        # 验签失败
    RATE_LIMITED = "RATE_LIMITED"                        # 触发限流
    ADAPTER_ERROR = "ADAPTER_ERROR"                      # 平台侧报错

    def __str__(self) -> str:
        return self.value


class RequestErrorCode(str, Enum):
    """入参/前置条件类错误。"""
    INVALID_PARAM = "INVALID_PARAM"                          # 参数不合法
    REVERSE_REASON_REQUIRED = "REVERSE_REASON_REQUIRED"      # 撤销核销未填原因
    ALREADY_REVERSED = "ALREADY_REVERSED"                    # 券未核销，无需撤销
    CONFIG_NOT_FOUND = "CONFIG_NOT_FOUND"                    # 平台配置不存在

    def __str__(self) -> str:
        return self.value


PLATFORM_ERROR_MESSAGES = {
    PlatformErrorCode.PLATFORM_NOT_INTEGRATED: "平台未接入",
    PlatformErrorCode.PLATFORM_UNKNOWN: "未知平台",
    PlatformErrorCode.PLATFORM_DISABLED: "平台配置已停用",
    PlatformErrorCode.SIGN_INVALID: "签名校验失败",
    PlatformErrorCode.RATE_LIMITED: "操作过于频繁，请稍后再试",
    PlatformErrorCode.ADAPTER_ERROR: "平台接口异常",
    RequestErrorCode.INVALID_PARAM: "参数不合法",
    RequestErrorCode.REVERSE_REASON_REQUIRED: "撤销核销必须填写原因",
    RequestErrorCode.ALREADY_REVERSED: "该券未核销，无需撤销",
    RequestErrorCode.CONFIG_NOT_FOUND: "平台配置不存在",
}


class GroupbuyError(Exception):
    """团购业务异常。

    由路由层捕获后转成标准响应体（``code`` 用错误码字符串本身），
    这样客户端拿到的 code 就是 ``COUPON_EXPIRED`` 这种能判别的值，
    而不是被压成一个笼统的「券无效」。
    """

    def __init__(self, code, message: str = "", data=None):
        code = str(code)
        if not message:
            message = (COUPON_ERROR_MESSAGES.get(code)
                       or PLATFORM_ERROR_MESSAGES.get(code) or code)
        super().__init__(message)
        self.code = code
        self.message = message
        self.data = data
