namespace YiHua.Pos.UI.Services.Ordering;

/// <summary>
/// 订单行输入（金额计算用）。
/// </summary>
public sealed class OrderLineInput
{
    /// <summary>数量（称重商品为重量 kg，可小数）</summary>
    public decimal Qty { get; set; } = 1m;

    /// <summary>单价（称重商品为每 kg 单价）</summary>
    public decimal Price { get; set; }

    /// <summary>行折扣额（≥0）</summary>
    public decimal LineDiscount { get; set; }

    /// <summary>是否赠菜（金额 0，但计入原价）</summary>
    public bool IsGift { get; set; }

    /// <summary>是否已退菜（不计入合计）</summary>
    public bool IsRefunded { get; set; }
}

/// <summary>
/// 整单金额计算结果。
/// </summary>
public sealed class OrderTotals
{
    /// <summary>原价合计（含赠菜原价、未扣退菜）—— 对账用</summary>
    public decimal OriginalTotal { get; set; }

    /// <summary>行折扣总额</summary>
    public decimal LineDiscountTotal { get; set; }

    /// <summary>行小计合计（扣行折扣、赠菜按 0、已退菜不计）</summary>
    public decimal Subtotal { get; set; }

    /// <summary>整单折扣额</summary>
    public decimal OrderDiscount { get; set; }

    /// <summary>抹零额（≥0）</summary>
    public decimal WipeZeroAmount { get; set; }

    /// <summary>应付金额</summary>
    public decimal Payable { get; set; }
}

/// <summary>
/// 金额计算器（纯逻辑，无 UI/IO 依赖）。
///
/// 计算规则：
/// 1. 行金额 = 数量 × 单价 − 行折扣（不低于 0）；赠菜行金额恒为 0。
/// 2. 称重商品：金额 = 重量(kg) × 单价，保留 2 位。
/// 3. 整单折扣：按折扣率作用于行小计合计。
/// 4. 抹零（可选）：直接抹掉「分」，¥128.57 → ¥128.50（向 0.1 取整，向下）。
/// 5. 退菜：已退行不计入小计；原价合计保留用于对账。
/// 6. 所有取整一律显式使用 MidpointRounding.AwayFromZero（收银不用银行家舍入）。
/// </summary>
public static class OrderCalculator
{
    /// <summary>
    /// 取整模式：远离零（四舍五入）。收银场景必须显式指定，不依赖默认值。
    /// </summary>
    public static readonly MidpointRounding RoundingMode = MidpointRounding.AwayFromZero;

    /// <summary>保留 2 位小数（AwayFromZero）</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, RoundingMode);

    /// <summary>
    /// 称重金额 = 重量(kg) × 单价，保留 2 位。
    /// 重量/单价为负时按 0 处理。
    /// </summary>
    public static decimal CalcWeightedAmount(decimal weightKg, decimal unitPrice)
    {
        if (weightKg <= 0m || unitPrice <= 0m)
            return 0m;
        return Round2(weightKg * unitPrice);
    }

    /// <summary>
    /// 行金额 = 数量 × 单价 − 行折扣（不低于 0，保留 2 位）。
    /// 赠菜行金额恒为 0。
    /// </summary>
    public static decimal CalcLineAmount(decimal qty, decimal price, decimal lineDiscount, bool isGift)
    {
        if (isGift)
            return 0m;
        if (qty <= 0m || price <= 0m)
            return 0m;
        decimal discount = lineDiscount < 0m ? 0m : lineDiscount;
        decimal amount = qty * price - discount;
        return Round2(amount < 0m ? 0m : amount);
    }

    /// <summary>
    /// 行金额（OrderLineInput 重载）。
    /// </summary>
    public static decimal CalcLineAmount(OrderLineInput line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        return CalcLineAmount(line.Qty, line.Price, line.LineDiscount, line.IsGift);
    }

    /// <summary>
    /// 整单计算。
    /// </summary>
    /// <param name="lines">订单行</param>
    /// <param name="orderDiscountRate">整单折扣率：1 = 不打折，0.9 = 九折；≤0 或 ≥1 视为不打折</param>
    /// <param name="wipeZero">抹零开关：true 则抹掉「分」（¥128.57 → ¥128.50）</param>
    public static OrderTotals CalcTotals(
        IEnumerable<OrderLineInput> lines,
        decimal orderDiscountRate = 1m,
        bool wipeZero = false)
    {
        var list = lines?.ToList() ?? new List<OrderLineInput>();
        var totals = new OrderTotals();

        foreach (var line in list)
        {
            // 原价合计：含赠菜原价（含已退，用于对账）
            totals.OriginalTotal += Round2(line.Qty * line.Price);

            if (line.IsRefunded)
                continue; // 退菜不计入小计

            decimal lineAmount = CalcLineAmount(line);
            totals.Subtotal += lineAmount;

            if (!line.IsGift && line.LineDiscount > 0m)
                totals.LineDiscountTotal += Round2(Math.Min(line.LineDiscount, line.Qty * line.Price));
        }

        totals.OriginalTotal = Round2(totals.OriginalTotal);
        totals.LineDiscountTotal = Round2(totals.LineDiscountTotal);
        totals.Subtotal = Round2(totals.Subtotal);

        // 整单折扣
        decimal rate = (orderDiscountRate > 0m && orderDiscountRate < 1m) ? orderDiscountRate : 1m;
        totals.OrderDiscount = Round2(totals.Subtotal * (1m - rate));

        decimal payable = totals.Subtotal - totals.OrderDiscount;

        // 抹零：抹掉「分」，向下取到 0.1
        if (wipeZero)
        {
            decimal wiped = WipeZero(payable);
            totals.WipeZeroAmount = Round2(payable - wiped);
            payable = wiped;
        }

        totals.Payable = Round2(payable < 0m ? 0m : payable);
        return totals;
    }

    /// <summary>
    /// 抹零：直接抹掉「分」，¥128.57 → ¥128.50（向下取到 0.1 元）。
    /// </summary>
    public static decimal WipeZero(decimal amount)
    {
        if (amount <= 0m) return 0m;
        return Math.Floor(amount * 10m) / 10m;
    }

    /// <summary>
    /// 找零 = 实收 − 应付（实收不足时返回负数，由调用方判断）。
    /// </summary>
    public static decimal CalcChange(decimal received, decimal payable)
    {
        return Round2(received - payable);
    }
}
