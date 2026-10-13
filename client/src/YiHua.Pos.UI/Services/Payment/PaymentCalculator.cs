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
    /// 抹零：**向下抹掉**，不是四舍五入。
    ///
    /// 抹分：抹掉分，保留到角 → ¥128.57 → ¥128.50
    /// 抹角：抹掉角，保留到元 → ¥128.50 → ¥128.00
    ///
    /// ⚠️ 这里必须用 Floor。用 Math.Round 会把 128.57 变成 128.60、
    /// 把 128.50 变成 129.00 —— 那是「往上凑整」，等于多收客人钱，
    /// 收银员还会被客人当场质疑。
    /// </summary>
    /// <param name="total">原价</param>
    /// <param name="toYuan">true=抹角（到元），false=抹分（到角）</param>
    /// <returns>(应收, 抹零额, 原价)</returns>
    public static (decimal Payable, decimal Discount, decimal Original) ApplyRoundingDiscount(decimal total, bool toYuan)
    {
        if (total <= 0m)
            return (0m, 0m, total);

        var payable = toYuan
            ? Math.Floor(total)
            : Math.Floor(total * 10m) / 10m;

        return (Round(payable), Round(total - payable), total);
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
/// 折扣类型。编号与参考端 t_order_discount.activityId 对齐，
/// 报表按活动分类统计优惠时才对得上。
/// </summary>
public enum DiscountType
{
    /// <summary>1 抹分（抹掉分，保留到角）：¥128.57 → ¥128.50</summary>
    RoundToJiao = 1,

    /// <summary>2 抹角（抹掉角，保留到元）：¥128.50 → ¥128.00</summary>
    RoundToYuan = 2,

    /// <summary>3 整单改价：直接把应收改成指定金额</summary>
    Override = 3,

    /// <summary>4 整单减免：减固定金额</summary>
    Fixed = 4,

    /// <summary>5 整单打折：按比例（0.9 = 九折）</summary>
    Rate = 5,

    /// <summary>6 赠菜（记在明细行上，不在这里算金额）</summary>
    Gift = 6,

    /// <summary>7 菜品打折（记在明细行上）</summary>
    ItemRate = 7,

    /// <summary>8 菜品减免（记在明细行上）</summary>
    ItemFixed = 8,

    /// <summary>9 支付优惠（第三方平台补贴，如扫码立减）</summary>
    Payment = 9,

    /// <summary>免单：整单 100% 优惠，实收 0</summary>
    Free = 100,
}

/// <summary>
/// 折扣申请（记录谁、何时、哪种折扣，用于审计与报表分类）。
/// </summary>
public class DiscountRequest
{
    public DiscountType Type { get; set; }

    /// <summary>Rate/ItemRate: 0.9=九折；Fixed/ItemFixed: 减免金额；Override: 改后的应收</summary>
    public decimal Value { get; set; }

    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>是否店长。折扣/抹零/免单是敏感操作，默认只有店长能做。</summary>
    public bool IsManager { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>参考端的 activityId（1~9），报表按它分类。</summary>
    public int ActivityId => (int)Type;
}
