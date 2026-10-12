using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.HangOrder;
using static YiHua.Pos.Tests.UI.HangOrder.HangOrderTestFactory;

namespace YiHua.Pos.Tests.UI.HangOrder;

public class HangOrderRecallTests
{
    private static async Task<(HangOrderRecallViewModel vm, FakeDeskRepository desks, FakeOrderRepository2 orders)>
        SetupConflictAsync()
    {
        var repo = new InMemoryHangOrderRepository();
        var desks = new FakeDeskRepository();
        var orders = new FakeOrderRepository2();

        var desk8 = DeskA08(); // 原桌
        var desk9 = DeskA09(); // 空闲桌
        desks.Desks.AddRange(new[] { desk8, desk9 });
        var order = SampleOrder();
        orders.Orders.Add(order);

        // 挂单
        var parkVm = new HangOrderParkViewModel(repo, desks, new HangOrderPrintService(null))
        {
            Order = order, Desk = desk8, OperatorName = "张三",
        };
        var record = await parkVm.ParkAsync();

        // 有人开了新单占用原桌
        desk8.Status = DeskStatus.Occupied;
        desk8.CurrentOrderId = 99;

        var vm = new HangOrderRecallViewModel(repo, desks, orders, new HangOrderPrintService(null))
        {
            Record = record,
        };
        return (vm, desks, orders);
    }

    [Fact]
    public async Task Recall_Conflict_When_Desk_Occupied()
    {
        var (vm, _, _) = await SetupConflictAsync();

        bool canRecall = await vm.CheckOriginalDeskAsync();

        Assert.False(canRecall);
        Assert.True(vm.HasConflict);
        Assert.NotNull(vm.ConflictMessage);
        Assert.Contains("占用", vm.ConflictMessage);
    }

    [Fact]
    public async Task Recall_Allows_Change_Desk()
    {
        var (vm, _, _) = await SetupConflictAsync();

        // 原桌被占，换到 A09（空闲）
        bool ok = await vm.RecallAsync(9);

        Assert.True(ok);
        Assert.False(vm.HasConflict);
    }

    [Fact]
    public async Task Recall_To_Busy_Desk_Rejected()
    {
        var (vm, desks, _) = await SetupConflictAsync();
        // A08 被占，尝试取单到 A08
        bool ok = await vm.RecallAsync(8);
        Assert.False(ok);
        Assert.True(vm.HasConflict);
    }

    [Fact]
    public async Task Recall_After_Change_Desk_Order_Updated()
    {
        var (vm, _, orders) = await SetupConflictAsync();
        await vm.RecallAsync(9);

        var order = orders.Orders.Single();
        Assert.Equal(9, order.DeskId); // 订单转到新桌
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
}
