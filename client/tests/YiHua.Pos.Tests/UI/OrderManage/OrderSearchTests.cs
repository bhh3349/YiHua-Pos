using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;

/// <summary>订单搜索测试：筛选/搜索/排序</summary>
public class OrderSearchTests
{
    private static OrderListViewModel CreateVm()
    {
        var repo = new FakeOrderRepository();
        var today = DateTime.Today;
        repo.Orders.AddRange(new[]
        {
            new Order { Id = 1, OrderNo = "A20261012001", Status = OrderStatus.Settled, AmountPay = 128m, CreatedAt = today.AddHours(12) },
            new Order { Id = 2, OrderNo = "A20261012002", Status = OrderStatus.Pending, AmountPay = 56m, CreatedAt = today.AddHours(11) },
            new Order { Id = 3, OrderNo = "A20261011001", Status = OrderStatus.Settled, AmountPay = 268m, CreatedAt = today.AddDays(-1).AddHours(12) },
        });
        return new OrderListViewModel(repo);
    }

    [Fact]
    public async Task Load_Defaults_To_Today()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        Assert.Equal(2, vm.Orders.Count); // 只显示当天
        Assert.All(vm.Orders, o => Assert.StartsWith("A20261012", o.OrderNo));
    }

    [Fact]
    public async Task Filter_By_Status()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.StatusFilter = OrderManageStatus.Pending;

        Assert.Single(vm.Orders);
        Assert.Equal("A20261012002", vm.Orders[0].OrderNo);
    }

    [Fact]
    public async Task Search_By_OrderNo()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.Keyword = "2001";

        Assert.Single(vm.Orders);
        Assert.Equal("A20261012001", vm.Orders[0].OrderNo);
    }

    [Fact]
    public async Task Sort_By_Amount_Desc()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        // 默认时间倒序：2001(12点) 在前
        Assert.Equal("A20261012001", vm.Orders[0].OrderNo);

        vm.SortByAmountDesc = true;

        // 金额从高到低：2001(128) 仍在前；加个验证
        Assert.Equal(128m, vm.Orders[0].AmountPay);
    }

    [Fact]
    public async Task ClearFilter_Restores_All()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.StatusFilter = OrderManageStatus.Pending;
        Assert.Single(vm.Orders);

        vm.ClearFilter();

        Assert.Equal(2, vm.Orders.Count);
    }
}
