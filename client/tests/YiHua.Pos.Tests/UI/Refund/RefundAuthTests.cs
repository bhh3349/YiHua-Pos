using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>权限与阈值：整单退单超阈值需店长授权，授权要记录</summary>
public class RefundAuthTests
{
    private readonly DefaultRefundPolicy _policy = new() { ManagerAuthThreshold = 200m };

    [Fact]
    public void Cashier_OverThreshold_Needs_Auth()
    {
        Assert.True(_policy.RequiresManagerAuth(RefundType.Order, 250m, "收银员"));
        Assert.False(_policy.RequiresManagerAuth(RefundType.Order, 150m, "收银员"));
    }

    [Fact]
    public void Manager_Never_Needs_Auth() =>
        Assert.False(_policy.RequiresManagerAuth(RefundType.Order, 1000m, "店长"));

    [Fact]
    public void ItemRefund_Never_Needs_Auth() =>
        Assert.False(_policy.RequiresManagerAuth(RefundType.Item, 500m, "收银员"));

    [Fact]
    public void Waiter_Cannot_RefundOrder()
    {
        var (allowed, reason) = _policy.CanPerform(RefundType.Order, "服务员");
        Assert.False(allowed);
        Assert.Contains("服务员不能整单退单", reason);
    }

    [Fact]
    public void Waiter_Cannot_Void()
    {
        var (allowed, reason) = _policy.CanPerform(RefundType.Void, "服务员");
        Assert.False(allowed);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void Chef_Cannot_Refund()
    {
        var (allowed, reason) = _policy.CanPerform(RefundType.Item, "厨师");
        Assert.False(allowed);
        Assert.Contains("厨师没有退款权限", reason);
    }

    [Fact]
    public void Auth_Records_ManagerName()
    {
        var vm = new RefundAuthViewModel(250m, 200m, "张三");
        Assert.False(vm.Validate()); // 未填店长名
        Assert.Contains("店长姓名", vm.Error);
        vm.ManagerName = "王店长";
        Assert.True(vm.Validate());
    }

    [Fact]
    public void AuthViewModel_Prompt_Shows_Amount()
    {
        var vm = new RefundAuthViewModel(250m, 200m, "张三");
        Assert.Contains("¥250.00", vm.PromptText);
        Assert.Contains("¥200.00", vm.PromptText);
    }

    [Fact]
    public async Task Refund_With_Auth_Records_AuthorizedBy()
    {
        var order = new YiHua.Pos.Domain.Entities.Order
        {
            Id = 1, Status = YiHua.Pos.Domain.Enums.OrderStatus.Settled,
            AmountReceived = 300m, AmountRefund = 0m,
        };
        var repo = new InMemoryRefundRepository();
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L, repo);
        Assert.True(vm.NeedsManagerAuth(250m)); // 超阈值需授权
        var (ok, _) = await vm.RefundOrderAsync(250m, "投诉", "王店长");
        Assert.True(ok);
        var records = await repo.GetByOrderIdAsync(1);
        Assert.Equal("王店长", records[0].AuthorizedBy); // 授权记录
    }
}
