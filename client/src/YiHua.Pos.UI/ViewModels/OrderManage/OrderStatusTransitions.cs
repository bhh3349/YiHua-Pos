using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.ViewModels.OrderManage;

/// <summary>
/// 订单管理状态（UI 层完整版）。
/// Pending/Settled/Refunded/Void 对应 Domain.OrderStatus；
/// Reversed（已冲正）、Skipped（已跳单）暂无 Domain 对应值，见 Domain 缺口清单。
/// </summary>
public enum OrderManageStatus
{
    Pending = 0,
    Settled = 1,
    Refunded = 2,
    Void = 3,
    Reversed = 4,
    Skipped = 5,
}

/// <summary>状态转换结果</summary>
public class TransitionResult
{
    public bool Ok { get; }
    public string Error { get; }
    private TransitionResult(bool ok, string error) { Ok = ok; Error = error; }
    public static TransitionResult Success() => new(true, string.Empty);
    public static TransitionResult Fail(string error) => new(false, error);
}

/// <summary>
/// 订单状态机：
/// 待结 ─结账→ 已结 ─反结账→ 已冲正
///           │└─退菜→ 已结(部分退)
///           └─跳单→ 已跳单 ─恢复→ 待结
/// </summary>
public static class OrderStatusTransitions
{
    /// <summary>结账：待结→已结</summary>
    public static TransitionResult TrySettle(OrderManageStatus current) =>
        current == OrderManageStatus.Pending
            ? TransitionResult.Success()
            : TransitionResult.Fail($"订单状态为{Describe(current)}，不能结账");

    /// <summary>
    /// 退菜：
    /// - 待结：允许，直接改明细
    /// - 已结：允许，生成退菜单（不改原单）
    /// - 已冲正/已作废：拒绝
    /// </summary>
    public static TransitionResult TryRefund(OrderManageStatus current, decimal refundQty, decimal orderedQty)
    {
        if (refundQty <= 0)
            return TransitionResult.Fail("退菜数量必须大于 0");
        if (refundQty > orderedQty)
            return TransitionResult.Fail($"退菜数量（{refundQty}）不能超过已点数量（{orderedQty}）");
        return current switch
        {
            OrderManageStatus.Pending => TransitionResult.Success(),
            OrderManageStatus.Settled => TransitionResult.Success(),
            OrderManageStatus.Reversed => TransitionResult.Fail("已冲正订单不能退菜"),
            OrderManageStatus.Void => TransitionResult.Fail("已作废订单不能退菜"),
            OrderManageStatus.Skipped => TransitionResult.Fail("已跳单订单请先恢复再退菜"),
            _ => TransitionResult.Fail($"订单状态为{Describe(current)}，不能退菜"),
        };
    }

    /// <summary>反结账：已结→已冲正（生成负数单，原单保留）</summary>
    public static TransitionResult TryReverse(OrderManageStatus current) =>
        current == OrderManageStatus.Settled
            ? TransitionResult.Success()
            : TransitionResult.Fail($"只有已结账订单能反结账，当前为{Describe(current)}");

    /// <summary>跳单：待结→已跳单</summary>
    public static TransitionResult TrySkip(OrderManageStatus current) =>
        current == OrderManageStatus.Pending
            ? TransitionResult.Success()
            : TransitionResult.Fail($"只有待结订单能跳单，当前为{Describe(current)}");

    /// <summary>恢复跳单：已跳单→待结</summary>
    public static TransitionResult TryUnskip(OrderManageStatus current) =>
        current == OrderManageStatus.Skipped
            ? TransitionResult.Success()
            : TransitionResult.Fail($"只有已跳单订单能恢复，当前为{Describe(current)}");

    /// <summary>重打：不改变状态，任何非作废订单都允许（补打凭证）</summary>
    public static TransitionResult TryReprint(OrderManageStatus current) =>
        current == OrderManageStatus.Void
            ? TransitionResult.Fail("已作废订单不能重打")
            : TransitionResult.Success();

    /// <summary>改数量（非退菜）：仅待结允许，已结账拒绝</summary>
    public static TransitionResult TryModifyQty(OrderManageStatus current) =>
        current == OrderManageStatus.Pending
            ? TransitionResult.Success()
            : TransitionResult.Fail($"已结账订单不能直接改数量，请走退菜流程");

    public static string Describe(OrderManageStatus s) => s switch
    {
        OrderManageStatus.Pending => "待结",
        OrderManageStatus.Settled => "已结",
        OrderManageStatus.Refunded => "已退",
        OrderManageStatus.Void => "已作废",
        OrderManageStatus.Reversed => "已冲正",
        OrderManageStatus.Skipped => "已跳单",
        _ => "未知",
    };
}
