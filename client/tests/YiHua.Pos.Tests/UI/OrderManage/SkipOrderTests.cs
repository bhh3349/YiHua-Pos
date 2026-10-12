using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;


/// <summary>跳单测试：可跳过、可恢复</summary>
public class SkipOrderTests
{
    [Fact]
    public async Task Skip_And_Restore()
    {
        var repo = new FakeOrderRepository();
        var order = new Order { Id = 1, OrderNo = "A01", Status = OrderStatus.Pending };
        var vm = new SkipOrderViewModel(repo, order) { Reason = "客人先不算" };
        bool skipped = false, restored = false;
        vm.Skipped += (s, e) => skipped = true;
        vm.Restored += (s, e) => restored = true;

        Assert.True(await vm.SkipAsync());
        Assert.True(skipped);
        Assert.True(await vm.RestoreAsync());
        Assert.True(restored);
    }

    [Fact]
    public async Task Skip_Requires_Reason()
    {
        var repo = new FakeOrderRepository();
        var order = new Order { Id = 1, OrderNo = "A01", Status = OrderStatus.Pending };
        var vm = new SkipOrderViewModel(repo, order);

        Assert.False(await vm.SkipAsync());
        Assert.Contains("原因", vm.ErrorMessage);
    }
}
