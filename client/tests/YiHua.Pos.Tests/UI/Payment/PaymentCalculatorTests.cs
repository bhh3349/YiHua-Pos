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
    public void ApplyRoundingDiscount_ToJiao()
    {
        var (payable, discount, original) = PaymentCalculator.ApplyRoundingDiscount(74.57m, toYuan: false);
        Assert.Equal(74.60m, payable); // AwayFromZero：74.57 → 74.6
        Assert.Equal(-0.03m, discount); // 注意：四舍五入可能为负（74.57→74.6 是加了 0.03）
        Assert.Equal(74.57m, original);
    }

    [Fact]
    public void ApplyRoundingDiscount_ToYuan()
    {
        var (payable, _, _) = PaymentCalculator.ApplyRoundingDiscount(74.57m, toYuan: true);
        Assert.Equal(75m, payable); // AwayFromZero
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
