using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Services.Refund;

// RefundType 已上移到 Domain（Wave 5 补充）。
/// <summary>
/// 退款计算器：纯逻辑类，所有金额红线集中于此，重点测试。
/// 全程 decimal，取整显式 MidpointRounding.AwayFromZero。
///
/// 金额红线：
/// - 退款 > 实收 → 拒绝
/// - 累计退款 > 实收 → 拒绝
/// - 退款为 0 或负数 → 拒绝
/// - 退菜金额 > 该行金额 → 拒绝
/// - 已全退后再退 → 拒绝
/// </summary>
public static class RefundCalculator
{
    /// <summary>
    /// 可退金额 = 实收 - 已退累计。
    /// 实收 = AmountReceived（现金实收）；已退累计 = AmountRefund。
    /// </summary>
    public static decimal CalcRefundable(Order order) =>
        Round(order.AmountReceived - order.AmountRefund);

    /// <summary>
    /// 退菜金额校验：不能超过该行金额（Price * Qty - 已退部分）。
    /// </summary>
    public static (bool Ok, string? Reason) ValidateItemRefund(OrderItem item, decimal amount)
    {
        if (amount <= 0)
            return (false, "退款金额必须大于 0");
        decimal lineAmount = Round(item.Price * item.Qty);
        if (item.IsRefunded)
            return (false, $"「{item.ProductName}」已退过，不能重复退");
        if (amount > lineAmount)
            return (false, $"退款 ¥{amount:F2} 超过该行金额 ¥{lineAmount:F2}");
        return (true, null);
    }

    /// <summary>
    /// 整单退款校验（含累计）。
    /// </summary>
    public static (bool Ok, string? Reason) ValidateOrderRefund(Order order, decimal amount)
    {
        if (amount <= 0)
            return (false, "退款金额必须大于 0");
        if (order.Status == OrderStatus.Refunded)
            return (false, "订单已全退，不能重复退款");
        decimal refundable = CalcRefundable(order);
        if (refundable <= 0)
            return (false, "订单已全退，不能重复退款");
        if (amount > refundable)
            return (false, $"退款 ¥{amount:F2} 超过可退金额 ¥{refundable:F2}（实收 ¥{order.AmountReceived:F2}，已退 ¥{order.AmountRefund:F2}）");
        return (true, null);
    }

    /// <summary>
    /// 退款后订单状态：全退 → Refunded，部分退 → 仍 Settled。
    /// </summary>
    public static OrderStatus CalcStatusAfterRefund(Order order, decimal newTotalRefund)
    {
        // 全退：累计退款 >= 实收（允许微小浮点误差，用 decimal 精确比较）
        if (newTotalRefund >= order.AmountReceived)
            return OrderStatus.Refunded;
        return OrderStatus.Settled;
    }

    /// <summary>
    /// 退款计入当前班次，不计入原班次（交班联动）。
    /// </summary>
    public static long ResolveShiftId(Order order, long currentShiftId) => currentShiftId;

    /// <summary>
    /// 冲正校验：只对未结账订单有效；已结账只能退单。
    /// </summary>
    public static (bool Ok, string? Reason) ValidateVoid(Order order)
    {
        return order.Status switch
        {
            OrderStatus.Pending => (true, null),
            OrderStatus.Settled => (false, "已结账订单不能冲正，请走退单流程"),
            OrderStatus.Refunded => (false, "已退款订单不能冲正"),
            OrderStatus.Void => (false, "订单已作废，不能重复操作"),
            _ => (false, "当前状态不能冲正"),
        };
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
