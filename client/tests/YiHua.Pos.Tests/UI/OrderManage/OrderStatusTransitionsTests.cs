using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;

/// <summary>订单状态机测试：合法+非法转换全覆盖</summary>
public class OrderStatusTransitionsTests
{
    [Fact]
    public void Settle_Pending_Succeeds() =>
        Assert.True(OrderStatusTransitions.TrySettle(OrderManageStatus.Pending).Ok);

    [Fact]
    public void Settle_Settled_Rejected() =>
        Assert.False(OrderStatusTransitions.TrySettle(OrderManageStatus.Settled).Ok);

    [Fact]
    public void Refund_Pending_Allowed() =>
        Assert.True(OrderStatusTransitions.TryRefund(OrderManageStatus.Pending, 1, 2).Ok);

    [Fact]
    public void Refund_Settled_Allowed() =>
        Assert.True(OrderStatusTransitions.TryRefund(OrderManageStatus.Settled, 1, 2).Ok);

    [Fact]
    public void Refund_Reversed_Rejected() =>
        Assert.False(OrderStatusTransitions.TryRefund(OrderManageStatus.Reversed, 1, 2).Ok);

    [Fact]
    public void Refund_OverQty_Rejected()
    {
        var r = OrderStatusTransitions.TryRefund(OrderManageStatus.Pending, 3, 2);
        Assert.False(r.Ok);
        Assert.Contains("超过", r.Error);
    }

    [Fact]
    public void Reverse_Settled_Succeeds() =>
        Assert.True(OrderStatusTransitions.TryReverse(OrderManageStatus.Settled).Ok);

    [Fact]
    public void Reverse_Pending_Rejected() =>
        Assert.False(OrderStatusTransitions.TryReverse(OrderManageStatus.Pending).Ok);

    [Fact]
    public void Reverse_Reversed_Rejected() =>
        Assert.False(OrderStatusTransitions.TryReverse(OrderManageStatus.Reversed).Ok);

    [Fact]
    public void Skip_Pending_Succeeds() =>
        Assert.True(OrderStatusTransitions.TrySkip(OrderManageStatus.Pending).Ok);

    [Fact]
    public void Skip_Settled_Rejected() =>
        Assert.False(OrderStatusTransitions.TrySkip(OrderManageStatus.Settled).Ok);

    [Fact]
    public void Unskip_Skipped_Succeeds() =>
        Assert.True(OrderStatusTransitions.TryUnskip(OrderManageStatus.Skipped).Ok);

    [Fact]
    public void Reprint_Void_Rejected() =>
        Assert.False(OrderStatusTransitions.TryReprint(OrderManageStatus.Void).Ok);

    [Fact]
    public void Reprint_Reversed_Allowed() =>
        Assert.True(OrderStatusTransitions.TryReprint(OrderManageStatus.Reversed).Ok);

    [Fact]
    public void ModifyQty_Settled_Rejected() =>
        Assert.False(OrderStatusTransitions.TryModifyQty(OrderManageStatus.Settled).Ok);

    [Fact]
    public void ModifyQty_Pending_Allowed() =>
        Assert.True(OrderStatusTransitions.TryModifyQty(OrderManageStatus.Pending).Ok);
}
