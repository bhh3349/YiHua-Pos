"""券验证状态机 —— 与平台无关的共通校验逻辑。

8 种券状态检查的判定顺序（先到先返回，收银端拿到的总是**最可操作的**
那个错误：券写错了先报「不存在」，而不是「已过期」）：

    1. COUPON_NOT_FOUND        券码不存在
    2. PLATFORM_MISMATCH       平台不匹配
    3. COUPON_USED             已核销
    4. COUPON_NOT_YET_VALID    未到有效期
    5. COUPON_EXPIRED          已过期
    6. COUPON_STORE_MISMATCH   门店不匹配
    7. COUPON_TIME_RESTRICTED  时段不符
    8. COUPON_NEED_BOOKING     需要预约

全部金额走 Decimal，禁止 float（任务卡 4.7）。
"""
import re
from datetime import datetime, time
from decimal import Decimal

from .errors import CouponErrorCode, GroupbuyError
from .platform import CouponInfo, CouponQuery, VerifyResult, ZERO

# 券码格式：4~64 位字母数字。太短/太长/带特殊字符直接打回 ——
# 既挡住注入，也避免把明显不是券码的东西送去平台查。
COUPON_CODE_RE = re.compile(r"^[A-Za-z0-9]{4,64}$")


def check_coupon_code_format(coupon_code: str) -> str:
    """券码格式校验，返回 strip 后的码。非法直接抛 GroupbuyError。"""
    v = (coupon_code or "").strip()
    if not v:
        raise GroupbuyError(CouponErrorCode.COUPON_NOT_FOUND, "券码不能为空")
    if not COUPON_CODE_RE.match(v):
        raise GroupbuyError(CouponErrorCode.COUPON_NOT_FOUND,
                            "券码格式非法（仅允许 4~64 位字母数字）")
    return v


def check_actual_discount(actual_discount: Decimal, coupon_discount: Decimal) -> Decimal:
    """实际抵扣金额校验：非负、不超过券面抵扣额。"""
    if actual_discount is None:
        return coupon_discount
    if actual_discount < ZERO:
        raise GroupbuyError("400", "实际抵扣金额不能为负数")
    if actual_discount > coupon_discount:
        raise GroupbuyError("400", "实际抵扣金额不能超过券面抵扣额 %s"
                                  % coupon_discount)
    return actual_discount


def verify_state(coupon, req: CouponQuery) -> VerifyResult:
    """对一张已取到的券做 8 项状态校验，全部通过返回 valid=True。

    ``coupon`` 是 :class:`app.models_groupbuy.GroupbuyCoupon`（自有平台）
    —— 判定只依赖字段值，不依赖具体平台，供各 adapter 复用。
    """
    EC = CouponErrorCode

    # 平台不匹配：券属于另一个平台
    if req.platform_code and coupon.platform_code != req.platform_code:
        return VerifyResult(False, error_code=str(EC.PLATFORM_MISMATCH),
                            message="券码与所选平台不匹配")

    # 已核销（撤销后状态回到待核销，可重新核销）
    if coupon.status == 2:  # COUPON_USED
        return VerifyResult(False, error_code=str(EC.COUPON_USED),
                            message="该券已核销，不能重复使用")

    now = req.biz_time or datetime.now()
    # 有效期：valid_from <= now <= valid_to
    if coupon.valid_from and now < coupon.valid_from:
        return VerifyResult(False, error_code=str(EC.COUPON_NOT_YET_VALID),
                            message="该券尚未生效，未到使用开始时间")
    if coupon.valid_to and now > coupon.valid_to:
        return VerifyResult(False, error_code=str(EC.COUPON_EXPIRED),
                            message="该券已过期")

    rst = coupon.restrictions or {}

    # 门店限制：restrictions.storeIds 非空时，当前门店必须在名单内
    allowed_stores = rst.get("storeIds") or []
    if allowed_stores and req.store_id not in [int(x) for x in allowed_stores]:
        return VerifyResult(False, error_code=str(EC.COUPON_STORE_MISMATCH),
                            message="该券不适用于当前门店")

    # 时段限制：restrictions.timeRanges = [{"start": "11:00", "end": "14:00"}, ...]
    ranges = rst.get("timeRanges") or []
    if ranges and not _in_time_ranges(now, ranges):
        return VerifyResult(False, error_code=str(EC.COUPON_TIME_RESTRICTED),
                            message="当前时段不可使用该券（如仅限午市）")

    # 预约限制：restrictions.needBooking 为真时必须已预约
    if rst.get("needBooking") and not req.booked:
        return VerifyResult(False, error_code=str(EC.COUPON_NEED_BOOKING),
                            message="该券需提前预约，请先完成预约")

    return VerifyResult(True, coupon=_to_info(coupon))


def _in_time_ranges(now: datetime, ranges) -> bool:
    """now 的时分是否落在任一 timeRanges 区间内（跨零点区间也支持）。"""
    t = now.time()
    for r in ranges:
        try:
            start = _parse_hhmm(r.get("start"))
            end = _parse_hhmm(r.get("end"))
        except Exception:
            continue
        if start <= end:
            if start <= t <= end:
                return True
        else:  # 跨零点：如 22:00~02:00
            if t >= start or t <= end:
                return True
    return False


def _parse_hhmm(s: str) -> time:
    hh, mm = str(s).split(":", 1)
    return time(int(hh), int(mm))


def _to_info(coupon) -> CouponInfo:
    return CouponInfo(
        coupon_code=coupon.coupon_code,
        platform_code=coupon.platform_code,
        platform_coupon_id=coupon.platform_coupon_id or "",
        coupon_name=coupon.coupon_name or "",
        coupon_type=coupon.coupon_type or "cash",
        face_value=Decimal(coupon.face_value or 0),
        discount_amount=Decimal(coupon.discount_amount or 0),
        valid_from=coupon.valid_from,
        valid_to=coupon.valid_to,
        restrictions=coupon.restrictions or {},
    )
