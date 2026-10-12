namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 券校验（纯逻辑）。
/// </summary>
public static class CouponValidator
{
    /// <summary>
    /// 校验门槛：订单金额必须 ≥ 券门槛。
    /// </summary>
    public static bool ValidateThreshold(decimal orderTotal, decimal threshold, out string? error)
    {
        if (orderTotal < threshold)
        {
            error = $"未达到用券门槛（满 ¥{threshold:F0} 可用，本单 ¥{orderTotal:F2}）";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 校验有效期。
    /// </summary>
    public static bool ValidateExpiry(DateTime expiry, DateTime now, out string? error)
    {
        if (now > expiry)
        {
            error = $"券已过期（有效期至 {expiry:yyyy-MM-dd}）";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 校验可核销：未用过、已绑定、是本人的。
    /// </summary>
    public static bool ValidateRedeemable(CouponDto coupon, long currentMemberId, out string? error)
    {
        if (coupon.IsUsed)
        {
            error = "这张券已经用过了";
            return false;
        }
        if (coupon.BoundMemberId == null)
        {
            error = "这张券还没发到会员，不能用";
            return false;
        }
        if (coupon.BoundMemberId != currentMemberId)
        {
            error = "这张券不是你的，不能用";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 完整校验（门槛+有效期+可核销）。
    /// </summary>
    public static bool Validate(CouponDto coupon, decimal orderTotal, long memberId, DateTime now, out string? error)
    {
        if (!ValidateThreshold(orderTotal, coupon.Threshold, out error)) return false;
        if (!ValidateExpiry(coupon.Expiry, now, out error)) return false;
        if (!ValidateRedeemable(coupon, memberId, out error)) return false;
        return true;
    }
}

/// <summary>
/// 券抵扣计算（纯逻辑）。
/// </summary>
public static class CouponCalculator
{
    /// <summary>
    /// 计算券抵扣金额。
    /// </summary>
    public static decimal CalcDiscount(CouponDto coupon, decimal orderTotal)
    {
        var discount = coupon.Type switch
        {
            CouponType.FullReduction => coupon.Value,                    // 满减：减固定金额
            CouponType.Discount => orderTotal * (1 - coupon.Value),      // 折扣：Value=0.9 表示 9折
            CouponType.Voucher => Math.Min(coupon.Value, orderTotal),   // 代金：不超过订单金额
            CouponType.Exchange => 0m,                                   // 兑换：不涉及金额
            _ => 0m,
        };
        return Round(Math.Max(0, Math.Min(discount, orderTotal)));
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
