using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Services.Shift;

/// <summary>
/// 班次计算器：纯逻辑类，所有金额计算集中于此，重点测试。
/// 取整一律 MidpointRounding.AwayFromZero。
///
/// 计算规则（明确）：
/// 1. 退菜退现金 → 现金应收减少（AmountRefund 视为现金退款，从现金收入中扣除）
/// 2. 冲正单（负数单）→ 直接计入净额（负数冲减总额）
/// 3. 抹零 → 计入优惠（AmountDiscount），NetAmount 用 AmountPay（已扣减）
/// 4. 记账收回 → 计入收款当班，不回溯原班（查询时按收款时间归属班次）
/// 5. 备用金不足 → 钱箱应有为负或偏小，差额自然为负（短款），由策略层判断是否需店长
/// </summary>
public static class ShiftCalculator
{
    /// <summary>
    /// 按支付方式汇总本班应收（仅已结账订单）。
    /// 现金：Σ现金收款 - Σ现金退款（退菜）。
    /// </summary>
    public static decimal SumByPayType(IEnumerable<Order> orders, PayType type)
    {
        decimal sum = 0;
        foreach (var o in orders.Where(o => o.Status == OrderStatus.Settled))
        {
            sum += o.Payments.Where(p => p.PayType == type).Sum(p => p.Amount);
            if (type == PayType.Cash)
                sum -= o.AmountRefund; // 退菜退现金：现金应收减少
        }
        return Round(sum);
    }

    /// <summary>现金收入（未扣退款）</summary>
    public static decimal CashIncome(IEnumerable<Order> orders) =>
        Round(orders.Where(o => o.Status == OrderStatus.Settled)
            .SelectMany(o => o.Payments)
            .Where(p => p.PayType == PayType.Cash)
            .Sum(p => p.Amount));

    /// <summary>现金退款（退菜）</summary>
    public static decimal CashRefund(IEnumerable<Order> orders) =>
        Round(orders.Where(o => o.Status == OrderStatus.Settled).Sum(o => o.AmountRefund));

    /// <summary>
    /// 钱箱应有 = 备用金 + 现金收入 - 现金退款。
    /// 备用金不足时结果偏小，差额为负即短款。
    /// </summary>
    public static decimal ExpectedCashInDrawer(decimal openingFloat, decimal cashIncome, decimal cashRefund) =>
        Round(openingFloat + cashIncome - cashRefund);

    /// <summary>
    /// 差额 = 实点 - 应有。
    /// 正=溢余（中性色），负=短款（警告色），零=持平。
    /// </summary>
    public static decimal CalculateDifference(decimal actualCash, decimal expectedCash) =>
        Round(actualCash - expectedCash);

    /// <summary>
    /// 本班净额 = Σ(已结订单 AmountPay) + Σ(冲正单 AmountPay，负数)。
    /// 冲正单直接计入，冲减总额。
    /// </summary>
    public static decimal NetAmount(IEnumerable<Order> orders) =>
        Round(orders.Where(o => o.Status is OrderStatus.Settled or OrderStatus.Void).Sum(o => o.AmountPay));

    /// <summary>本班优惠总额（含抹零）</summary>
    public static decimal TotalDiscount(IEnumerable<Order> orders) =>
        Round(orders.Where(o => o.Status == OrderStatus.Settled).Sum(o => o.AmountDiscount));

    /// <summary>本班退菜笔数与金额</summary>
    public static (int Count, decimal Amount) RefundStats(IEnumerable<Order> orders)
    {
        var settled = orders.Where(o => o.Status == OrderStatus.Settled && o.AmountRefund > 0).ToList();
        return (settled.Count, Round(settled.Sum(o => o.AmountRefund)));
    }

    /// <summary>本班订单数（已结）</summary>
    public static int OrderCount(IEnumerable<Order> orders) =>
        orders.Count(o => o.Status == OrderStatus.Settled);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
