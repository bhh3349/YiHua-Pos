namespace YiHua.Pos.UI.Services.Payment;

/// <summary>
/// 支付金额计算器（纯逻辑）。
/// 取整规则：显式使用 MidpointRounding.AwayFromZero（收银不是银行家舍入）。
/// </summary>
public static class PaymentCalculator
{
    /// <summary>
    /// 按比例折扣（如 9折 = 0.9）。
    /// </summary>
    /// <param name="total">原价</param>
    /// <param name="rate">折扣率（0.9 表示 9折）</param>
    /// <returns>(应收, 折扣额)</returns>
    public static (decimal Payable, decimal Discount) ApplyRateDiscount(decimal total, decimal rate)
    {
        if (rate < 0m || rate > 1m)
            throw new ArgumentOutOfRangeException(nameof(rate), "折扣率必须在 0~1 之间");
        var payable = Round(total * rate);
        return (payable, total - payable);
    }

    /// <summary>
    /// 直接减（如减 10 元）。
    /// </summary>
    public static (decimal Payable, decimal Discount) ApplyFixedDiscount(decimal total, decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "减免金额不能为负");
        var discount = Math.Min(amount, total);
        return (total - discount, discount);
    }

    /// <summary>
    /// 抹零：抹掉分（保留到角）或抹到元。
    /// </summary>
    /// <param name="total">原价</param>
    /// <param name="toYuan">true=抹到元，false=抹到角</param>
    /// <returns>(应收, 抹零额, 原价)</returns>
    public static (decimal Payable, decimal Discount, decimal Original) ApplyRoundingDiscount(decimal total, bool toYuan)
    {
        var payable = toYuan
            ? Math.Round(total, 0, MidpointRounding.AwayFromZero)
            : Math.Round(total, 1, MidpointRounding.AwayFromZero);
        return (payable, total - payable, total);
    }

    /// <summary>
    /// 计算找零。实收必须 >= 应收，否则抛异常。
    /// </summary>
    public static decimal CalculateChange(decimal received, decimal payable)
    {
        if (received < payable)
            throw new InvalidOperationException($"实收 ¥{received:F2} 不足应收 ¥{payable:F2}");
        return Round(received - payable);
    }

    /// <summary>
    /// 金额取整到分（AwayFromZero）。
    /// </summary>
    public static decimal Round(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 校验实收是否足够（可测试）。
    /// </summary>
    public static bool IsSufficient(decimal received, decimal payable)
        => received >= payable;

    /// <summary>
    /// 快捷金额：返回常用的实收选项（50/100/200/整单）。
    /// </summary>
    public static List<decimal> GetQuickAmounts(decimal payable)
    {
        var amounts = new List<decimal> { payable };
        foreach (var q in new[] { 50m, 100m, 200m, 500m })
        {
            if (q >= payable && !amounts.Contains(q))
                amounts.Add(q);
        }
        return amounts.OrderBy(a => a).ToList();
    }
}

/// <summary>
/// 折扣类型。
/// </summary>
public enum DiscountType
{
    /// <summary>按比例（如 9折）。</summary>
    Rate,
    /// <summary>直接减（如减 10 元）。</summary>
    Fixed,
    /// <summary>抹零到角。</summary>
    RoundToJiao,
    /// <summary>抹零到元。</summary>
    RoundToYuan,
}

/// <summary>
/// 折扣申请（记录谁、何时、哪种折扣，用于审计）。
/// </summary>
public class DiscountRequest
{
    public DiscountType Type { get; set; }
    public decimal Value { get; set; } // Rate: 0.9=9折；Fixed: 金额
    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public bool IsManager { get; set; }
    public string Reason { get; set; } = string.Empty;
}
