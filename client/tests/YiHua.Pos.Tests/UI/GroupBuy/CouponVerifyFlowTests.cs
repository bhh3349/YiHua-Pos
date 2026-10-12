using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.GroupBuy;
using YiHua.Pos.UI.ViewModels.GroupBuy;
using Xunit;

namespace YiHua.Pos.Tests.UI.GroupBuy;

/// <summary>完整验券→核销流程</summary>
public class CouponVerifyFlowTests
{
    private static (Order, FakeGroupBuyClient) Setup()
    {
        var order = new Order { Id = 1, OrderNo = "A08", AmountPay = 100m };
        var fake = new FakeGroupBuyClient();
        fake.AddCoupon(new CouponInfo
        {
            CouponCode = "MT123ABC",
            PlatformCode = "meituan",
            CouponName = "30元代金券",
            FaceValue = 30m,
            DiscountAmount = 30m,
        });
        return (order, fake);
    }

    [Fact]
    public async Task Verify_Then_Redeem_Writes_Order()
    {
        var (order, fake) = Setup();
        var vm = new CouponVerifyViewModel(order, 1, "张三", fake);
        vm.InputCode = " mt123abc \n"; // 带空格小写 → 归一化
        await vm.VerifyAsync();
        Assert.Equal(VerifyState.Success, vm.State);
        Assert.Equal(30m, vm.Coupon!.DiscountAmount);
        Assert.Equal(70m, vm.PayableAfter); // 100 - 30

        bool ok = await vm.RedeemAsync();
        Assert.True(ok);
        Assert.Equal("美团", order.GroupBuyPlatform); // 中文平台名
        Assert.Equal("MT123ABC", order.GroupBuyCouponCode);
        Assert.Equal(30m, order.AmountGroupBuy); // 抵扣额写回
    }

    [Fact]
    public async Task Redeem_Twice_Only_Once()
    {
        // 重复点击核销只成功一次
        var (order, fake) = Setup();
        var vm = new CouponVerifyViewModel(order, 1, "张三", fake);
        vm.InputCode = "MT123ABC";
        await vm.VerifyAsync();
        bool first = await vm.RedeemAsync();
        bool second = await vm.RedeemAsync();
        Assert.True(first);
        Assert.False(second); // 第二次被拦
        Assert.True(vm.Redeemed);
    }

    [Fact]
    public async Task Verify_Empty_Code_Rejected()
    {
        var (order, fake) = Setup();
        var vm = new CouponVerifyViewModel(order, 1, "张三", fake);
        vm.InputCode = "   ";
        await vm.VerifyAsync();
        Assert.Equal(VerifyState.Error, vm.State);
        Assert.Contains("券码为空", vm.ErrorMessage);
    }

    [Fact]
    public async Task Verify_Unknown_Coupon_Human_Message()
    {
        var (order, fake) = Setup();
        var vm = new CouponVerifyViewModel(order, 1, "张三", fake);
        vm.InputCode = "NOTEXIST";
        await vm.VerifyAsync();
        Assert.Equal(VerifyState.Error, vm.State);
        Assert.Contains("不存在", vm.ErrorMessage); // 人话，不是 COUPON_NOT_FOUND
    }

    [Fact]
    public async Task Verify_Discount_Over_Payable_Rejected()
    {
        var order = new Order { Id = 1, AmountPay = 20m }; // 应收 20
        var fake = new FakeGroupBuyClient();
        fake.AddCoupon(new CouponInfo { CouponCode = "BIG", PlatformCode = "meituan", FaceValue = 50m, DiscountAmount = 50m });
        var vm = new CouponVerifyViewModel(order, 1, "张三", fake);
        vm.InputCode = "BIG";
        await vm.VerifyAsync();
        Assert.Equal(VerifyState.Error, vm.State);
        Assert.Contains("超过应收", vm.ErrorMessage);
    }

    [Fact]
    public async Task Redeem_Failure_Does_Not_Modify_Order()
    {
        // 核销失败不能改订单
        var (order, fake) = Setup();
        var vm = new CouponVerifyViewModel(order, 1, "张三", new FailingClient());
        vm.InputCode = "MT123ABC";
        // FailingClient 验券直接失败
        await vm.VerifyAsync();
        Assert.Equal(VerifyState.Error, vm.State);
        Assert.Equal(string.Empty, order.GroupBuyCouponCode); // 订单未被改
        Assert.Equal(0m, order.AmountGroupBuy);
    }

    private class FailingClient : IGroupBuyClient
    {
        public Task<CouponVerifyResult> VerifyAsync(string p, string c, long s, CancellationToken ct = default) =>
            Task.FromResult(new CouponVerifyResult { Valid = false, ErrorCode = "COUPON_USED" });
        public Task<CouponRedeemResult> RedeemAsync(string p, string c, long s, long o, CancellationToken ct = default) =>
            Task.FromResult(new CouponRedeemResult { Success = false, ErrorCode = "REDEEM_FAILED" });
        public Task<bool> ReverseAsync(string p, string c, long s, string r, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
