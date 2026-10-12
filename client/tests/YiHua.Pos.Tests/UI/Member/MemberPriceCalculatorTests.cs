using YiHua.Pos.UI.Services.Member;
using Xunit;

namespace YiHua.Pos.Tests.UI.Member;

public class MemberPriceCalculatorTests
{
    private static List<MemberOrderItem> MakeItems() => new()
    {
        new() { ProductName = "牛肉面", Price = 22m, MemberPrice = 18m, Qty = 2 },
        new() { ProductName = "可乐", Price = 5m, MemberPrice = null, Qty = 1 },
    };

    private static MemberDto MakeMember() => new()
    {
        Id = 1, Name = "张三", Phone = "13812345678", Balance = 100m,
    };

    [Fact]
    public void Calc_MemberPrice_HighestPriority()
    {
        // 单品会员价 > 会员折扣：即使有 9折，也用会员价
        var result = MemberPriceCalculator.Calc(
            MakeItems(), MakeMember(),
            memberDiscountRate: 0.9m, normalDiscountRate: null, coupon: null);

        // 牛肉面 18*2=36，可乐 5*1=5，总计 41
        Assert.Equal(41m, result.Payable);
        Assert.Equal("会员价", result.AppliedLabel);
    }

    [Fact]
    public void Calc_MemberDiscount_SecondPriority()
    {
        // 无单品会员价时，用整单会员折扣
        var items = new List<MemberOrderItem>
        {
            new() { ProductName = "牛肉面", Price = 22m, MemberPrice = null, Qty = 2 },
        };
        var result = MemberPriceCalculator.Calc(
            items, MakeMember(),
            memberDiscountRate: 0.9m, normalDiscountRate: 0.8m, coupon: null);

        // 会员折扣优先于普通折扣：22*2*0.9=39.6
        Assert.Equal(39.6m, result.Payable);
        Assert.Contains("会员", result.AppliedLabel);
    }

    [Fact]
    public void Calc_Coupon_MutuallyExclusiveWithMemberDiscount()
    {
        var coupon = new CouponDto
        {
            Code = "C001", Name = "满50减10",
            Type = CouponType.FullReduction, Threshold = 50m, Value = 10m,
        };
        var items = new List<MemberOrderItem>
        {
            new() { ProductName = "牛肉面", Price = 22m, MemberPrice = 18m, Qty = 3 }, // 原价 66
        };
        // 有券时，会员价和会员折扣都不用
        var result = MemberPriceCalculator.Calc(
            items, MakeMember(),
            memberDiscountRate: 0.9m, normalDiscountRate: null, coupon: coupon);

        Assert.Equal(56m, result.Payable); // 66-10
        Assert.Contains("券", result.AppliedLabel);
    }

    [Fact]
    public void Calc_MemberPriceHigherThanNormal_Throws()
    {
        var items = new List<MemberOrderItem>
        {
            new() { ProductName = "牛肉面", Price = 22m, MemberPrice = 25m, Qty = 1 },
        };
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MemberPriceCalculator.Calc(items, MakeMember(), null, null, null));
        Assert.Contains("高于原价", ex.Message);
    }

    [Fact]
    public void Calc_NonMember_NoDiscount()
    {
        var result = MemberPriceCalculator.Calc(
            MakeItems(), null, null, null, null);
        // 非会员：22*2+5=49
        Assert.Equal(49m, result.Payable);
        Assert.Equal("无折扣", result.AppliedLabel);
    }

    [Fact]
    public void Calc_Rounding_AwayFromZero()
    {
        // 9.95 * 0.9 = 8.955 → 8.96 (AwayFromZero，不是 8.95)
        var items = new List<MemberOrderItem>
        {
            new() { ProductName = "测试", Price = 9.95m, Qty = 1 },
        };
        var result = MemberPriceCalculator.Calc(
            items, MakeMember(), memberDiscountRate: 0.9m,
            normalDiscountRate: null, coupon: null);
        Assert.Equal(8.96m, result.Payable);
    }

    [Fact]
    public void CanApplyBoth_AlwaysFalse()
    {
        Assert.False(MemberPriceCalculator.CanApplyBoth());
    }

    [Fact]
    public void Calc_MemberPriceDisabled_FallsBack()
    {
        // 会员价开关关闭时，不用会员价
        var result = MemberPriceCalculator.Calc(
            MakeItems(), MakeMember(),
            memberDiscountRate: null, normalDiscountRate: null, coupon: null,
            memberPriceEnabled: false);
        Assert.Equal(49m, result.Payable); // 原价
    }
}
