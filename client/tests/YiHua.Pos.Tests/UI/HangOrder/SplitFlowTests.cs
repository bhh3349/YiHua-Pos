using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;
using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.HangOrder;
using static YiHua.Pos.Tests.UI.HangOrder.HangOrderTestFactory;

namespace YiHua.Pos.Tests.UI.HangOrder;

/// <summary>完整预结账流程：挂单 → 拆单 → 多次预结账 → 全结</summary>
public class SplitFlowTests
{
    [Fact]
    public async Task Full_Flow_Park_Split_Split_Settle()
    {
        var repo = new InMemoryHangOrderRepository();
        var orderRepo = new FakeOrderRepository2();
        var deskRepo = new FakeDeskRepository();
        var desk = DeskA08();
        deskRepo.Desks.Add(desk);
        var order = SampleOrder(); // 101:36, 102:38, 共 74
        orderRepo.Orders.Add(order);

        // 1. 挂单
        var parkVm = new HangOrderParkViewModel(repo, deskRepo, new HangOrderPrintService(null))
        {
            Order = order, Desk = desk, OperatorName = "张三",
        };
        var record = await parkVm.ParkAsync();
        Assert.NotNull(record);
        Assert.Equal(DeskStatus.Idle, desk.Status);

        // 2. 第一次预结账：拆 101（¥36）
        var splitVm = new SplitOrderViewModel(repo, orderRepo);
        await splitVm.LoadAsync(record.Id);
        Assert.NotNull(splitVm.Splitter);
        splitVm.Splitter.ToggleSelect(101);
        Assert.Equal(36m, splitVm.Splitter.SplitAmount);
        Assert.Equal(38m, splitVm.Splitter.RemainingAmount);
        Assert.True(await splitVm.ConfirmSplitAsync());
        Assert.Equal(OrderStatus.Pending, order.Status); // 还有未结
        Assert.Single(record.SettledItemIds);

        // 3. 第二次预结账：拆 102（¥38）
        var splitVm2 = new SplitOrderViewModel(repo, orderRepo);
        await splitVm2.LoadAsync(record.Id);
        Assert.Single(splitVm2.Splitter!.AvailableItems); // 只剩 102
        splitVm2.Splitter.ToggleSelect(102);
        Assert.True(await splitVm2.ConfirmSplitAsync());
        Assert.Equal(OrderStatus.Settled, order.Status); // 全结
        Assert.Equal(2, record.SettledItemIds.Count);
    }

    [Fact]
    public async Task Split_After_Full_Settle_No_Available()
    {
        var repo = new InMemoryHangOrderRepository();
        var orderRepo = new FakeOrderRepository2();
        var order = SampleOrder();
        orderRepo.Orders.Add(order);

        var record = new HangOrderRecord
        {
            DeskId = 8, DeskName = "A08", OrderId = order.Id,
            SettledItemIds = new List<long> { 101, 102 }, // 已全结
        };
        await repo.ParkAsync(record);

        var splitVm = new SplitOrderViewModel(repo, orderRepo);
        await splitVm.LoadAsync(record.Id);

        Assert.NotNull(splitVm.Splitter);
        Assert.Empty(splitVm.Splitter.AvailableItems);
    }
}
