using YiHua.Pos.UI.Services.GroupBuy;
using Xunit;

namespace YiHua.Pos.Tests.UI.GroupBuy;

/// <summary>优惠口径 + 抵扣上限（最重要）</summary>
public class GroupBuyCalculatorTests
{
    [Fact]
    public void Apply_Discount_Goes_To_Discount_Not_Payment()
    {
        // 原价 100，抵 30，实收 70
        var (discount, pay) = GroupBuyCalculator.Apply(100m, 30m);
        Assert.Equal(30m, discount);  // 进 AmountDiscount
        Assert.Equal(70m, pay);       // 实收
    }

    [Fact]
    public void ValidateDiscount_OverPayable_Rejected()
    {
        var (ok, err) = GroupBuyCalculator.ValidateDiscount(120m, 100m);
        Assert.False(ok);
        Assert.Contains("超过应收", err);
    }

    [Fact]
    public void ValidateDiscount_EqualPayable_Allowed()
    {
        // 抵扣 = 应收 → 允许，应收变 0
        var (ok, _) = GroupBuyCalculator.ValidateDiscount(100m, 100m);
        Assert.True(ok);
        var (_, pay) = GroupBuyCalculator.Apply(100m, 100m);
        Assert.Equal(0m, pay);
    }

    [Fact]
    public void ValidateDiscount_ZeroPayable_Rejected()
    {
        var (ok, err) = GroupBuyCalculator.ValidateDiscount(30m, 0m);
        Assert.False(ok);
        Assert.Contains("应收为 0", err);
    }

    [Fact]
    public void ValidateDiscount_ZeroDiscount_Rejected()
    {
        var (ok, _) = GroupBuyCalculator.ValidateDiscount(0m, 100m);
        Assert.False(ok);
    }

    [Fact]
    public void PickDiscount_Uses_DiscountAmount_Not_FaceValue()
    {
        // 面值 50，实际抵扣 30 → 用 30
        var coupon = new CouponInfo { FaceValue = 50m, DiscountAmount = 30m };
        Assert.Equal(30m, GroupBuyCalculator.PickDiscount(coupon));
    }

    [Fact]
    public void Rounding_AwayFromZero()
    {
        // 9.995 → 10.00（AwayFromZero），不是 9.99
        var (discount, _) = GroupBuyCalculator.Apply(100m, 9.995m);
        Assert.Equal(10.00m, discount);
    }

    [Fact]
    public void GetGroupBuyDiscount_Returns_AmountGroupBuy()
    {
        var order = new YiHua.Pos.Domain.Entities.Order { AmountGroupBuy = 30m };
        Assert.Equal(30m, GroupBuyCalculator.GetGroupBuyDiscount(order));
    }
}
