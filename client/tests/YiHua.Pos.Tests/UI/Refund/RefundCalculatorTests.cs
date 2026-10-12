using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>金额计算与全部边界</summary>
public class RefundCalculatorTests
{
    private static Order MakeOrder(decimal received = 128m, decimal refunded = 0m, OrderStatus status = OrderStatus.Settled) =>
        new() { Id = 1, OrderNo = "A08", Status = status, AmountReceived = received, AmountRefund = refunded };

    [Fact]
    public void Refundable_Equals_Received_Minus_Refunded() =>
        Assert.Equal(100m, RefundCalculator.CalcRefundable(MakeOrder(128m, 28m)));

    [Fact]
    public void ItemRefund_OverLineAmount_Rejected()
    {
        var item = new OrderItem { ProductName = "牛肉面", Price = 18m, Qty = 1 };
        var (ok, reason) = RefundCalculator.ValidateItemRefund(item, 20m);
        Assert.False(ok);
        Assert.Contains("超过该行金额", reason);
    }

    [Fact]
    public void ItemRefund_ExactLineAmount_Ok()
    {
        var item = new OrderItem { ProductName = "牛肉面", Price = 18m, Qty = 2 };
        var (ok, _) = RefundCalculator.ValidateItemRefund(item, 36m);
        Assert.True(ok);
    }

    [Fact]
    public void ItemRefund_AlreadyRefunded_Rejected()
    {
        var item = new OrderItem { ProductName = "饮料", Price = 8m, Qty = 1, IsRefunded = true };
        var (ok, reason) = RefundCalculator.ValidateItemRefund(item, 8m);
        Assert.False(ok);
        Assert.Contains("已退过", reason);
    }

    [Fact]
    public void OrderRefund_OverReceived_Rejected()
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(MakeOrder(128m), 150m);
        Assert.False(ok);
        Assert.Contains("超过可退金额", reason);
    }

    [Fact]
    public void OrderRefund_Zero_Rejected()
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(MakeOrder(128m), 0m);
        Assert.False(ok);
        Assert.Contains("大于 0", reason);
    }

    [Fact]
    public void OrderRefund_Negative_Rejected()
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(MakeOrder(128m), -10m);
        Assert.False(ok);
    }

    [Fact]
    public void OrderRefund_FullyRefunded_Rejected()
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(MakeOrder(128m, 128m, OrderStatus.Refunded), 10m);
        Assert.False(ok);
        Assert.Contains("已全退", reason);
    }

    [Fact]
    public void StatusAfterRefund_Full_Then_Refunded() =>
        Assert.Equal(OrderStatus.Refunded, RefundCalculator.CalcStatusAfterRefund(MakeOrder(128m), 128m));

    [Fact]
    public void StatusAfterRefund_Partial_Then_Settled() =>
        Assert.Equal(OrderStatus.Settled, RefundCalculator.CalcStatusAfterRefund(MakeOrder(128m), 26m));

    [Fact]
    public void ResolveShiftId_Uses_Current_Shift() =>
        Assert.Equal(99L, RefundCalculator.ResolveShiftId(MakeOrder(), 99L));

    [Fact]
    public void Void_Pending_Ok()
    {
        var (ok, _) = RefundCalculator.ValidateVoid(MakeOrder(status: OrderStatus.Pending));
        Assert.True(ok);
    }

    [Fact]
    public void Void_Settled_Rejected()
    {
        var (ok, reason) = RefundCalculator.ValidateVoid(MakeOrder(status: OrderStatus.Settled));
        Assert.False(ok);
        Assert.Contains("退单", reason);   // 实现文案是「已结账订单不能冲正，请走退单流程」
    }

    [Fact]
    public void Decimal_Precision_NoFloat()
    {
        // 0.1 + 0.2 精度测试
        var order = MakeOrder(0.3m, 0m);
        var (ok, _) = RefundCalculator.ValidateOrderRefund(order, 0.1m + 0.2m);
        Assert.True(ok); // decimal 精确，不会因浮点误差拒绝
    }

    [Fact]
    public void Rounding_AwayFromZero()
    {
        // 2.345 → 2.35（AwayFromZero），不是银行家舍入的 2.34
        var item = new OrderItem { ProductName = "测试", Price = 2.345m, Qty = 1 };
        Assert.Equal(2.35m, Math.Round(item.Price * item.Qty, 2, MidpointRounding.AwayFromZero));
    }
}
