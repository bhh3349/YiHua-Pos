using YiHua.Pos.UI.Services.Payment;
using Xunit;

namespace YiHua.Pos.Tests.UI.Payment;

/// <summary>
/// 支付计算器测试：折扣/抹零/找零/取整规则。
/// </summary>
public class PaymentCalculatorTests
{
    [Fact]
    public void ApplyRateDiscount_NineOff_Correct()
    {
        var (payable, discount) = PaymentCalculator.ApplyRateDiscount(74m, 0.9m);
        Assert.Equal(66.60m, payable);
        Assert.Equal(7.40m, discount);
    }

    [Fact]
    public void ApplyRateDiscount_InvalidRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentCalculator.ApplyRateDiscount(100m, 1.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentCalculator.ApplyRateDiscount(100m, -0.1m));
    }

    [Fact]
    public void ApplyFixedDiscount_Correct()
    {
        var (payable, discount) = PaymentCalculator.ApplyFixedDiscount(74m, 10m);
        Assert.Equal(64m, payable);
        Assert.Equal(10m, discount);
    }

    [Fact]
    public void ApplyFixedDiscount_CappedAtTotal()
    {
        var (payable, discount) = PaymentCalculator.ApplyFixedDiscount(50m, 100m);
        Assert.Equal(0m, payable);
        Assert.Equal(50m, discount); // 不能减成负数
    }

    [Fact]
    public void ApplyRoundingDiscount_抹分_向下抹掉分()
    {
        var (payable, discount, original) = PaymentCalculator.ApplyRoundingDiscount(74.57m, toYuan: false);

        // 抹分 = 抹掉分，保留到角：74.57 → 74.50
        Assert.Equal(74.50m, payable);
        Assert.Equal(0.07m, discount);
        Assert.Equal(74.57m, original);

        // 优惠额永远是正的：抹零只会让客人少付，不会多付
        Assert.True(discount >= 0m);
    }

    [Fact]
    public void ApplyRoundingDiscount_抹角_向下抹掉角()
    {
        var (payable, discount, _) = PaymentCalculator.ApplyRoundingDiscount(74.57m, toYuan: true);

        // 抹角 = 抹掉角，保留到元：74.57 → 74.00
        Assert.Equal(74m, payable);
        Assert.Equal(0.57m, discount);
        Assert.True(discount >= 0m);
    }

    [Fact]
    public void ApplyRoundingDiscount_抹零后不会比原价高()
    {
        // 这是收银口径的底线：抹零是让利，不是往上凑整。
        // 之前用四舍五入，74.57 抹到角变成 74.60 —— 多收客人 3 分钱。
        foreach (var amount in new[] { 0.01m, 0.05m, 38.55m, 74.57m, 128.99m })
        {
            foreach (var toYuan in new[] { false, true })
            {
                var (payable, discount, _) = PaymentCalculator.ApplyRoundingDiscount(amount, toYuan);
                Assert.True(payable <= amount, $"{amount} 抹零后变成 {payable}");
                Assert.True(discount >= 0m, $"{amount} 抹零产生了负优惠 {discount}");
            }
        }
    }

    [Fact]
    public void ApplyRoundingDiscount_AlreadyRound_NoChange()
    {
        var (payable, discount, _) = PaymentCalculator.ApplyRoundingDiscount(74.50m, toYuan: false);
        Assert.Equal(74.50m, payable);
        Assert.Equal(0m, discount);
    }

    [Fact]
    public void CalculateChange_Correct()
    {
        Assert.Equal(26m, PaymentCalculator.CalculateChange(100m, 74m));
    }

    [Fact]
    public void CalculateChange_Insufficient_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PaymentCalculator.CalculateChange(50m, 74m));
        Assert.Contains("不足", ex.Message);
    }

    [Fact]
    public void CalculateChange_Exact_NoChange()
    {
        Assert.Equal(0m, PaymentCalculator.CalculateChange(74m, 74m));
    }

    [Fact]
    public void IsSufficient_ChecksCorrectly()
    {
        Assert.True(PaymentCalculator.IsSufficient(100m, 74m));
        Assert.True(PaymentCalculator.IsSufficient(74m, 74m));
        Assert.False(PaymentCalculator.IsSufficient(50m, 74m));
    }

    [Fact]
    public void Round_UsesAwayFromZero()
    {
        // AwayFromZero：2.555 → 2.56（不是银行家舍入的 2.55）
        Assert.Equal(2.56m, PaymentCalculator.Round(2.555m));
        Assert.Equal(2.55m, PaymentCalculator.Round(2.554m));
    }

    [Fact]
    public void GetQuickAmounts_IncludesPayableAndLarger()
    {
        var amounts = PaymentCalculator.GetQuickAmounts(74m);
        Assert.Contains(74m, amounts);
        Assert.Contains(100m, amounts);
        Assert.DoesNotContain(50m, amounts); // 50 < 74，不包含
        Assert.Equal(amounts.OrderBy(a => a).ToList(), amounts); // 升序
    }
}
