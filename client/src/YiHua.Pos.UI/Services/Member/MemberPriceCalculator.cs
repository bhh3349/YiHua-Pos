namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 订单行（会员价计算用）。
/// </summary>
public class MemberOrderItem
{
    public string ProductName { get; set; } = string.Empty;
    /// <summary>原价。</summary>
    public decimal Price { get; set; }
    /// <summary>会员价（null=无会员价）。</summary>
    public decimal? MemberPrice { get; set; }
    public decimal Qty { get; set; } = 1;
}

/// <summary>
/// 折扣计算结果。
/// </summary>
public class DiscountResult
{
    /// <summary>最终应收。</summary>
    public decimal Payable { get; set; }
    /// <summary>总折扣额。</summary>
    public decimal Discount { get; set; }
    /// <summary>用了哪种折扣（用于显示）。</summary>
    public string AppliedLabel { get; set; } = string.Empty;
    /// <summary>明细：每行的原价→折后价。</summary>
    public List<(string ProductName, decimal Original, decimal Final)> Details { get; } = new();
}

/// <summary>
/// 会员价计算器（纯逻辑）。
///
/// 折扣优先级（顺序不能错）：
///   单品会员价 > 会员折扣（整单） > 普通折扣 > 抹零
///
/// 规则：
/// - 会员价与券互斥：用券就取消会员价
/// - 会员价高于普通价 → 拒绝启用（配置错误）
/// - 取整显式 AwayFromZero，全程 decimal
/// </summary>
public static class MemberPriceCalculator
{
    /// <summary>
    /// 计算订单折扣。
    /// </summary>
    /// <param name="items">订单行</param>
    /// <param name="member">会员（null=非会员）</param>
    /// <param name="memberDiscountRate">会员整单折扣率（如 0.9），null=不启用</param>
    /// <param name="normalDiscountRate">普通折扣率，null=不启用</param>
    /// <param name="coupon">优惠券，null=不用券</param>
    /// <param name="memberPriceEnabled">会员价开关（可临时关闭）</param>
    public static DiscountResult Calc(
        List<MemberOrderItem> items,
        MemberDto? member,
        decimal? memberDiscountRate,
        decimal? normalDiscountRate,
        CouponDto? coupon,
        bool memberPriceEnabled = true)
    {
        var result = new DiscountResult();
        var originalTotal = items.Sum(i => i.Price * i.Qty);

        // 1. 单品会员价（最高优先级）
        if (member != null && memberPriceEnabled && coupon == null)
        {
            var memberPriceTotal = 0m;
            var hasMemberPrice = false;
            foreach (var item in items)
            {
                ValidateMemberPrice(item);
                var final = item.MemberPrice ?? item.Price;
                if (item.MemberPrice != null) hasMemberPrice = true;
                memberPriceTotal += final * item.Qty;
                result.Details.Add((item.ProductName, item.Price * item.Qty, Round(final * item.Qty)));
            }
            if (hasMemberPrice)
            {
                result.Payable = Round(memberPriceTotal);
                result.Discount = Round(originalTotal - memberPriceTotal);
                result.AppliedLabel = "会员价";
                return result;
            }
        }

        // 2. 会员折扣（整单）— 与券互斥
        if (member != null && memberDiscountRate != null && coupon == null)
        {
            var payable = Round(originalTotal * memberDiscountRate.Value);
            foreach (var item in items)
                result.Details.Add((item.ProductName, item.Price * item.Qty, Round(item.Price * item.Qty * memberDiscountRate.Value)));
            result.Payable = payable;
            result.Discount = Round(originalTotal - payable);
            result.AppliedLabel = $"会员 {memberDiscountRate.Value * 10:F1} 折";
            return result;
        }

        // 3. 券（与会员折扣互斥）
        if (coupon != null)
        {
            var discount = CouponCalculator.CalcDiscount(coupon, originalTotal);
            var payable = Round(originalTotal - discount);
            foreach (var item in items)
                result.Details.Add((item.ProductName, item.Price * item.Qty, item.Price * item.Qty));
            result.Payable = payable;
            result.Discount = Round(discount);
            result.AppliedLabel = $"券：{coupon.Name}";
            return result;
        }

        // 4. 普通折扣
        if (normalDiscountRate != null)
        {
            var payable = Round(originalTotal * normalDiscountRate.Value);
            foreach (var item in items)
                result.Details.Add((item.ProductName, item.Price * item.Qty, Round(item.Price * item.Qty * normalDiscountRate.Value)));
            result.Payable = payable;
            result.Discount = Round(originalTotal - payable);
            result.AppliedLabel = $"折扣 {normalDiscountRate.Value * 10:F1} 折";
            return result;
        }

        // 无折扣
        foreach (var item in items)
            result.Details.Add((item.ProductName, item.Price * item.Qty, item.Price * item.Qty));
        result.Payable = Round(originalTotal);
        result.Discount = 0;
        result.AppliedLabel = "无折扣";
        return result;
    }

    /// <summary>
    /// 会员价与券是否互斥（始终互斥）。
    /// </summary>
    public static bool CanApplyBoth() => false;

    /// <summary>
    /// 校验会员价配置：会员价不能高于普通价。
    /// </summary>
    public static void ValidateMemberPrice(MemberOrderItem item)
    {
        if (item.MemberPrice != null && item.MemberPrice > item.Price)
            throw new InvalidOperationException(
                $"「{item.ProductName}」会员价 ¥{item.MemberPrice:F2} 高于原价 ¥{item.Price:F2}，配置错误，请检查");
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
