namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>
/// 团购抵扣计算器：纯逻辑，重点测试。
///
/// 金额口径（最重要）：
/// - 抵扣进 AmountDiscount（优惠），不进实收
/// - 订单原价 100，团购券抵 30，实收（现金/微信）70
/// - ❌ 不能记成收款「团购 70 元」——平台结的是核销金额 30，不是实付 70
///
/// 全程 decimal，取整显式 MidpointRounding.AwayFromZero，禁止 float。
/// </summary>
public static class GroupBuyCalculator
{
    /// <summary>
    /// 应用团购抵扣：返回（优惠金额，实收金额）。
    /// 抵扣进优惠，不进实收。
    /// </summary>
    public static (decimal AmountDiscount, decimal AmountPay) Apply(decimal orderTotal, decimal discount)
    {
        discount = Round(discount);
        decimal pay = Round(orderTotal - discount);
        return (discount, pay);
    }

    /// <summary>
    /// 抵扣校验：抵扣不能超过应收；= 应收允许（应收变 0）；应收为 0 拒绝用券。
    /// </summary>
    public static (bool Ok, string? Error) ValidateDiscount(decimal discount, decimal payable)
    {
        if (payable <= 0)
            return (false, "应收为 0，不能使用团购券");
        if (discount <= 0)
            return (false, "抵扣金额必须大于 0");
        if (discount > payable)
            return (false, $"抵扣 ¥{discount:F2} 超过应收 ¥{payable:F2}");
        return (true, null);
    }

    /// <summary>
    /// 用服务端 discountAmount，不用 faceValue。
    /// 平台按实际核销金额结账，用面值对不上账。
    /// </summary>
    public static decimal PickDiscount(CouponInfo coupon) => coupon.DiscountAmount;

    /// <summary>
    /// 接入 Ordering 的钩子：计算团购抵扣额。
    /// OrderEntryViewModel 第 537 行：
    ///   AmountDiscount = totals.LineDiscountTotal + totals.OrderDiscount + totals.WipeZeroAmount
    /// 合并时加第四项：
    ///   AmountDiscount = ... + GroupBuyCalculator.GetGroupBuyDiscount(order)
    /// 本卡不改 Ordering/ 目录，计算放这里，报告里说明。
    /// </summary>
    public static decimal GetGroupBuyDiscount(YiHua.Pos.Domain.Entities.Order order) =>
        Round(order.AmountGroupBuy);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
