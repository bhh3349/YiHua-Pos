using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>后厨撤单：退菜联动厨房，可重复打印</summary>
public class RefundKitchenTests
{
    [Fact]
    public async Task ItemRefund_Notifies_Kitchen()
    {
        bool notified = false;
        var kitchen = new KitchenNotifyService((record, items) =>
        {
            notified = true;
            Assert.Equal(RefundType.Item, record.Type);
            Assert.Equal(2, items.Count);
            return Task.FromResult(true);
        });

        var order = new Order
        {
            Id = 1, Status = OrderStatus.Settled, AmountReceived = 128m,
            Items = new List<OrderItem>
            {
                new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 1 },
                new() { Id = 2, ProductName = "饮料", Price = 8m, Qty = 1 },
            },
        };
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L,
            new InMemoryRefundRepository(), null, kitchen);
        var items = new List<RefundableItem>
        {
            new(order.Items[0]) { Selected = true },
            new(order.Items[1]) { Selected = true },
        };
        var (ok, error) = await vm.RefundItemsAsync(items, "客人不要了");
        Assert.True(ok);
        Assert.Null(error);
        Assert.True(notified); // 后厨已通知
    }

    [Fact]
    public async Task Kitchen_Print_Failure_Still_Refunds()
    {
        // 打印失败不影响退款本身，但要提示
        var kitchen = new KitchenNotifyService((_, _) => Task.FromResult(false));
        var order = new Order
        {
            Id = 1, Status = OrderStatus.Settled, AmountReceived = 128m,
            Items = new List<OrderItem> { new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 1 } },
        };
        var repo = new InMemoryRefundRepository();
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L, repo, null, kitchen);
        var items = new List<RefundableItem> { new(order.Items[0]) { Selected = true } };
        var (ok, error) = await vm.RefundItemsAsync(items, "上错菜");
        Assert.True(ok); // 退款成功
        Assert.Contains("打印失败", error); // 但提示打印问题
    }

    [Fact]
    public async Task Reprint_RefundMenu()
    {
        // 退菜单可重复打印（厨房漏单补打）
        int printCount = 0;
        var kitchen = new KitchenNotifyService((_, _) =>
        {
            printCount++;
            return Task.FromResult(true);
        });
        var record = new RefundRecord { Id = 1, OrderId = 1, Type = RefundType.Item, Amount = 26m };
        var items = new List<OrderItem> { new() { ProductName = "牛肉面", Price = 18m, Qty = 1 } };

        var (ok1, _) = await kitchen.NotifyCancelAsync(record, items);
        var (ok2, _) = await kitchen.ReprintAsync(record, items);
        Assert.True(ok1 && ok2);
        Assert.Equal(2, printCount); // 打印了两次
    }

    [Fact]
    public void RefundReason_Required()
    {
        // 退菜必须填原因
        var vm = new RefundReasonViewModel();
        Assert.True(vm.Validate()); // 默认有原因
        vm.Reason = RefundReason.Other;
        Assert.False(vm.Validate()); // 选"其他"无说明 → 拒绝
        Assert.Contains("补充说明", vm.Error);
        vm.Note = "客人过敏";
        Assert.True(vm.Validate());
    }
}
