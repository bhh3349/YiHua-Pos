using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>退款状态机：状态流转</summary>
public class RefundStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Settled, true)]
    [InlineData(OrderStatus.Refunded, false)]
    [InlineData(OrderStatus.Void, false)]
    [InlineData(OrderStatus.Pending, false)]
    public void CanRefundItem_Status(OrderStatus status, bool expected)
    {
        var (ok, _) = RefundStateMachine.CanRefundItem(status, false);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void CanRefundItem_AlreadyRefundedLine_Rejected()
    {
        var (ok, reason) = RefundStateMachine.CanRefundItem(OrderStatus.Settled, true);
        Assert.False(ok);
        Assert.Contains("已退过", reason);
    }

    [Theory]
    [InlineData(OrderStatus.Settled, 50, true)]
    [InlineData(OrderStatus.Settled, 0, false)] // 已全退
    [InlineData(OrderStatus.Refunded, 50, false)]
    [InlineData(OrderStatus.Pending, 50, false)] // 未结账走冲正
    [InlineData(OrderStatus.Void, 50, false)]
    public void CanRefundOrder_Status(OrderStatus status, decimal refundable, bool expected)
    {
        var (ok, _) = RefundStateMachine.CanRefundOrder(status, refundable);
        Assert.Equal(expected, ok);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Settled, false)]
    [InlineData(OrderStatus.Refunded, false)]
    [InlineData(OrderStatus.Void, false)]
    public void CanVoid_Status(OrderStatus status, bool expected)
    {
        var (ok, _) = RefundStateMachine.CanVoid(status);
        Assert.Equal(expected, ok);
    }
}
