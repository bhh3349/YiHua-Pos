using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.HangOrder;
using static YiHua.Pos.Tests.UI.HangOrder.HangOrderTestFactory;

namespace YiHua.Pos.Tests.UI.HangOrder;

/// <summary>桌台状态与挂单记录的独立性（本卡最容易做错的点）</summary>
public class HangOrderDeskTests
{
    [Fact]
    public async Task Park_Desk_Becomes_Idle_But_Record_Kept()
    {
        var repo = new InMemoryHangOrderRepository();
        var deskRepo = new FakeDeskRepository();
        var desk = DeskA08();
        deskRepo.Desks.Add(desk);
        var order = SampleOrder();

        var parkVm = new HangOrderParkViewModel(repo, deskRepo, new HangOrderPrintService(null))
        {
            Order = order,
            Desk = desk,
            OperatorName = "张三",
            Remark = "等朋友",
        };

        var record = await parkVm.ParkAsync();

        Assert.NotNull(record);
        // 关键断言 1：桌台变空闲（可开新单）
        Assert.Equal(DeskStatus.Idle, desk.Status);
        Assert.Equal(0, desk.CurrentOrderId);
        // 关键断言 2：挂单记录独立存在
        Assert.Equal(1, await repo.CountAsync());
        var saved = await repo.GetByIdAsync(record.Id);
        Assert.NotNull(saved);
        Assert.Equal(8, saved.DeskId);
        Assert.Equal("A08", saved.DeskName);
        Assert.Equal(1, saved.OrderId);
        Assert.Equal("等朋友", saved.Remark);
    }

    [Fact]
    public async Task Parked_Desk_Can_Take_New_Order()
    {
        var repo = new InMemoryHangOrderRepository();
        var deskRepo = new FakeDeskRepository();
        var desk = DeskA08();
        deskRepo.Desks.Add(desk);

        // 挂单
        var parkVm = new HangOrderParkViewModel(repo, deskRepo, new HangOrderPrintService(null))
        {
            Order = SampleOrder(),
            Desk = desk,
            OperatorName = "张三",
        };
        await parkVm.ParkAsync();

        // 桌台空闲，可开新单
        Assert.Equal(DeskStatus.Idle, desk.Status);

        // 模拟开新单
        desk.Status = DeskStatus.Occupied;
        desk.CurrentOrderId = 2;

        // 挂单记录仍在（独立）
        Assert.Equal(1, await repo.CountAsync());
        var records = await repo.GetByDeskIdAsync(8);
        Assert.Single(records);
    }

    [Fact]
    public async Task Recall_Restores_Desk_Occupied()
    {
        var repo = new InMemoryHangOrderRepository();
        var deskRepo = new FakeDeskRepository();
        var orderRepo = new FakeOrderRepository2();
        var desk = DeskA08();
        deskRepo.Desks.Add(desk);
        var order = SampleOrder();
        orderRepo.Orders.Add(order);

        // 先挂单
        var parkVm = new HangOrderParkViewModel(repo, deskRepo, new HangOrderPrintService(null))
        {
            Order = order, Desk = desk, OperatorName = "张三",
        };
        var record = await parkVm.ParkAsync();
        Assert.Equal(DeskStatus.Idle, desk.Status);

        // 再取单（原桌空闲）
        var recallVm = new HangOrderRecallViewModel(repo, deskRepo, orderRepo, new HangOrderPrintService(null))
        {
            Record = record,
        };
        Assert.True(await recallVm.CheckOriginalDeskAsync());
        Assert.True(await recallVm.RecallAsync(8));

        // 桌台恢复占用
        Assert.Equal(DeskStatus.Occupied, desk.Status);
        Assert.Equal(1, desk.CurrentOrderId);
        // 订单回 Pending
        Assert.Equal(OrderStatus.Pending, order.Status);
        // 挂单记录已移除
        Assert.Equal(0, await repo.CountAsync());
    }
}
