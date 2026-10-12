using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>边界：超额/负数/零/重复退款（走完整 ViewModel 流程）</summary>
public class RefundBoundaryTests
{
    private static Order MakeOrder(decimal received = 128m, decimal refunded = 0m, OrderStatus status = OrderStatus.Settled) =>
        new() { Id = 1, OrderNo = "A08", Status = status, AmountReceived = received, AmountRefund = refunded };

    private static RefundOrderViewModel MakeVm(Order order) =>
        new(order, "张三", "收银员", 10L);

    [Fact]
    public async Task CumulativeRefund_OverReceived_Rejected()
    {
        var order = MakeOrder(128m, 100m); // 已退 100，还可退 28
        var vm = MakeVm(order);
        var (ok, error) = await vm.RefundOrderAsync(50m, "客人投诉", null);
        Assert.False(ok);
        Assert.Contains("超过可退金额", error);
    }

    [Fact]
    public async Task MultipleRefunds_Accumulate_Correctly()
    {
        var order = MakeOrder(128m);
        var vm = MakeVm(order);
        var (ok1, _) = await vm.RefundOrderAsync(50m, "部分退", null);
        var (ok2, _) = await vm.RefundOrderAsync(78m, "剩余", null);
        Assert.True(ok1 && ok2);
        Assert.Equal(128m, order.AmountRefund);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public async Task Refund_Zero_Rejected()
    {
        var vm = MakeVm(MakeOrder());
        var (ok, error) = await vm.RefundOrderAsync(0m, "测试", null);
        Assert.False(ok);
        Assert.Contains("大于 0", error);
    }

    [Fact]
    public async Task Refund_Negative_Rejected()
    {
        var vm = MakeVm(MakeOrder());
        var (ok, _) = await vm.RefundOrderAsync(-5m, "测试", null);
        Assert.False(ok);
    }

    [Fact]
    public async Task Refund_AfterFull_Rejected()
    {
        var order = MakeOrder(128m, 128m, OrderStatus.Refunded);
        var vm = MakeVm(order);
        var (ok, error) = await vm.RefundOrderAsync(10m, "再退", null);
        Assert.False(ok);
        Assert.Contains("已全退", error);
    }

    [Fact]
    public async Task ItemRefund_OverLineAmount_Rejected()
    {
        var order = MakeOrder(128m);
        order.Items = new List<OrderItem> { new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 1 } };
        var vm = MakeVm(order);
        var items = new List<RefundableItem> { new(order.Items[0]) { Selected = true, RefundAmount = 25m } };
        var (ok, error) = await vm.RefundItemsAsync(items, "上错菜");
        Assert.False(ok);
        Assert.Contains("超过该行金额", error);
    }

    [Fact]
    public async Task ItemRefund_AfterOrderRefunded_Rejected()
    {
        // 整单退完后又退菜 → 拒绝
        var order = MakeOrder(128m, 128m, OrderStatus.Refunded);
        order.Items = new List<OrderItem> { new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 1 } };
        var vm = MakeVm(order);
        var items = new List<RefundableItem> { new(order.Items[0]) { Selected = true } };
        var (ok, _) = await vm.RefundItemsAsync(items, "再退菜");
        Assert.False(ok);
    }

    [Fact]
    public async Task ItemRefund_Keeps_Partial_Status()
    {
        // 退菜后订单不变成"已退单"，而是部分退款
        var order = MakeOrder(128m);
        order.Items = new List<OrderItem> { new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 1 } };
        var vm = MakeVm(order);
        var items = new List<RefundableItem> { new(order.Items[0]) { Selected = true } };
        var (ok, _) = await vm.RefundItemsAsync(items, "客人不要了");
        Assert.True(ok);
        Assert.Equal(OrderStatus.Settled, order.Status); // 仍是 Settled，不是 Refunded
        Assert.Equal(18m, order.AmountRefund);
    }
}
