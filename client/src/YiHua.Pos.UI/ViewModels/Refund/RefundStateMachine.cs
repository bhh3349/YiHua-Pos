using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>
/// 退款状态机：纯逻辑。
/// 退菜（部分）→ 订单保持 Settled（部分退款）；
/// 整单退完 → Refunded；
/// 冲正 → 仅 Pending 可作废为 Void。
/// </summary>
public static class RefundStateMachine
{
    /// <summary>是否可以退菜（已结账未全退）</summary>
    public static (bool Ok, string? Reason) CanRefundItem(OrderStatus status, bool alreadyRefunded)
    {
        if (alreadyRefunded)
            return (false, "该行已退过，不能重复退");
        return status switch
        {
            OrderStatus.Settled => (true, null),
            OrderStatus.Refunded => (false, "订单已全退，不能再退菜"),
            OrderStatus.Void => (false, "已作废订单不能退菜"),
            OrderStatus.Pending => (false, "未结账订单请走点单退菜流程"),
            _ => (false, "当前状态不能退菜"),
        };
    }

    /// <summary>是否可以整单退单</summary>
    public static (bool Ok, string? Reason) CanRefundOrder(OrderStatus status, decimal refundable)
    {
        return status switch
        {
            OrderStatus.Settled when refundable > 0 => (true, null),
            OrderStatus.Settled => (false, "订单已全退，不能重复退款"),
            OrderStatus.Refunded => (false, "订单已全退，不能重复退款"),
            OrderStatus.Void => (false, "已作废订单不能退单"),
            OrderStatus.Pending => (false, "未结账订单不能退单（请走冲正）"),
            _ => (false, "当前状态不能退单"),
        };
    }

    /// <summary>是否可以冲正</summary>
    public static (bool Ok, string? Reason) CanVoid(OrderStatus status) =>
        RefundCalculator.ValidateVoid(
            new YiHua.Pos.Domain.Entities.Order { Status = status });
}
