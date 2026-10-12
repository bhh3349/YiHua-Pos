using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;
using OrderingFakes = YiHua.Pos.Tests.UI.Ordering;

namespace YiHua.Pos.Tests.UI.FastFood;

public class FastFoodFlowTests
{
    private static (FastFoodEntryViewModel vm, OrderingFakes.FakeOrderRepository orders)
        CreateVm()
    {
        var products = new OrderingFakes.FakeProductRepository();
        products.Products.Add(new YiHua.Pos.Domain.Entities.Product
        {
            Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
            CategoryId = 10, ProductType = 1, DataStatus = 1,
        });
        var cats = new OrderingFakes.FakeCategoryRepository();
        var orders = new OrderingFakes.FakeOrderRepository();
        var outbox = new OrderingFakes.FakeOutboxRepository();
        var tickets = new TicketQueueStateMachine(new InMemoryTicketQueue());
        var vm = new FastFoodEntryViewModel(products, cats, orders, outbox, tickets);
        return (vm, orders);
    }

    [Fact]
    public void IssueTicket_Binds_Number()
    {
        var (vm, _) = CreateVm();

        vm.IssueTicket(3);

        Assert.Equal("A001", vm.TicketNumber);
        Assert.Equal("取号 A001 · 3 位", vm.OrderTitleText);
        Assert.True(vm.HasTicket);
    }

    [Fact]
    public async Task Submit_Without_Ticket_Fails()
    {
        var (vm, _) = CreateVm();
        vm.AddProduct(new YiHua.Pos.Domain.Entities.Product
        {
            Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
            ProductType = 1, DataStatus = 1,
        });

        var result = await vm.SubmitAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("取号", result.Error);
    }

    [Fact]
    public async Task Full_Flow_Issue_Order_Pay_Complete()
    {
        var (vm, orders) = CreateVm();

        // 1. 取号
        vm.IssueTicket(2);
        Assert.Equal("A001", vm.TicketNumber);

        // 2. 点单
        vm.AddProduct(new YiHua.Pos.Domain.Entities.Product
        {
            Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
            ProductType = 1, DataStatus = 1,
        });
        Assert.Equal(18m, vm.Totals.Payable);

        // 3. 提交
        var result = await vm.SubmitAsync();
        Assert.True(result.IsSuccess);
        var order = orders.Orders.Single();
        Assert.Equal("A001", order.TakeNo);
        Assert.Equal(0, order.DeskId); // 快餐无桌台
        Assert.Equal(OrderType.DineIn, order.OrderType);

        // 4. 未结账不能完成
        Assert.False(vm.TryCompleteTicket(out string? reason));
        Assert.NotNull(reason);
        Assert.Contains("结账", reason);

        // 5. 结账后可以完成
        vm.MarkSettled();
        Assert.True(vm.TryCompleteTicket(out _));
        Assert.False(vm.HasTicket); // 清理完毕
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void OrderTitle_Shows_Ticket_Not_Desk()
    {
        var (vm, _) = CreateVm();
        // 快餐无桌台概念，标题只显示取号
        Assert.Equal("未取号", vm.OrderTitleText);
        vm.IssueTicket(1);
        Assert.DoesNotContain("桌", vm.OrderTitleText);
        Assert.Contains("A001", vm.OrderTitleText);
    }

    [Fact]
    public void OrderType_Selection()
    {
        var (vm, _) = CreateVm();
        vm.IssueTicket(1);

        vm.OrderType = OrderType.TakeAway;
        Assert.Equal(OrderType.TakeAway, vm.OrderType);
        Assert.Equal(OrderType.TakeAway, vm.Takeaway.OrderType);

        vm.OrderType = OrderType.Delivery;
        Assert.Equal(OrderType.Delivery, vm.Takeaway.OrderType);
    }

    [Fact]
    public async Task TakeAway_Tableware_In_Remark()
    {
        var (vm, orders) = CreateVm();
        vm.IssueTicket(1);
        vm.OrderType = OrderType.TakeAway;
        vm.Takeaway.TablewareCount = 3;
        vm.AddProduct(new YiHua.Pos.Domain.Entities.Product
        {
            Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
            ProductType = 1, DataStatus = 1,
        });

        await vm.SubmitAsync();

        var order = orders.Orders.Single();
        Assert.Contains("餐具×3", order.Remark);
    }
}
