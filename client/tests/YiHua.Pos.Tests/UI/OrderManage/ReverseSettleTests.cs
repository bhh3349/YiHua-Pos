using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;


/// <summary>反结账测试：冲正不改原单、权限</summary>
public class ReverseSettleTests
{
    [Fact]
    public async Task Reverse_Creates_Negative_Order_Keeps_Original()
    {
        var repo = new FakeOrderRepository();
        var order = new Order
        {
            Id = 1, OrderNo = "A20261012001", Status = OrderStatus.Settled,
            AmountTotal = 128m, AmountPay = 128m,
        };
        var vm = new ReverseSettleViewModel(repo, order) { Reason = "算错了" };

        bool ok = await vm.ConfirmAsync("店长");

        Assert.True(ok);
        Assert.NotNull(repo.LastInserted);
        Assert.Equal(-128m, repo.LastInserted!.AmountPay); // 负数单
        Assert.Contains("A20261012001", repo.LastInserted.OrderNo);
        Assert.Equal(128m, order.AmountPay);               // 原单金额不动
        Assert.Contains("已冲正", order.Remark);
    }

    [Fact]
    public async Task Reverse_NonManager_Rejected()
    {
        var repo = new FakeOrderRepository();
        var order = new Order { Id = 1, OrderNo = "X", Status = OrderStatus.Settled, AmountPay = 10m };
        var vm = new ReverseSettleViewModel(repo, order) { Reason = "x" };

        bool ok = await vm.ConfirmAsync("收银员");

        Assert.False(ok);
        Assert.Contains("店长", vm.ErrorMessage);
    }

    [Fact]
    public async Task Reverse_Requires_Reason()
    {
        var repo = new FakeOrderRepository();
        var order = new Order { Id = 1, OrderNo = "X", Status = OrderStatus.Settled, AmountPay = 10m };
        var vm = new ReverseSettleViewModel(repo, order);

        bool ok = await vm.ConfirmAsync("店长");

        Assert.False(ok);
        Assert.Contains("原因", vm.ErrorMessage);
    }
}
