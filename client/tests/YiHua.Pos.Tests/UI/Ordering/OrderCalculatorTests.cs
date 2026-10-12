using Xunit;
using YiHua.Pos.UI.Services.Ordering;

namespace YiHua.Pos.Tests.UI.Ordering;

public class OrderCalculatorTests
{
    // ---------- 基础取整 ----------

    [Fact]
    public void RoundingMode_Is_AwayFromZero()
    {
        // 显式要求：收银不用银行家舍入
        Assert.Equal(MidpointRounding.AwayFromZero, OrderCalculator.RoundingMode);
    }

    [Fact]
    public void Round2_HalfUp_Not_Banker()
    {
        // 2.345 → 2.35（AwayFromZero）；银行家舍入会给 2.34
        Assert.Equal(2.35m, OrderCalculator.Round2(2.345m));
        Assert.Equal(2.34m, OrderCalculator.Round2(2.344m));
    }

    // ---------- 行金额 ----------

    [Fact]
    public void CalcLineAmount_Basic()
    {
        Assert.Equal(64.00m, OrderCalculator.CalcLineAmount(2, 32m, 0m, false));
    }

    [Fact]
    public void CalcLineAmount_With_LineDiscount()
    {
        // 2 × 32 − 4 = 60
        Assert.Equal(60.00m, OrderCalculator.CalcLineAmount(2, 32m, 4m, false));
    }

    [Fact]
    public void CalcLineAmount_Discount_NotBelowZero()
    {
        Assert.Equal(0m, OrderCalculator.CalcLineAmount(1, 10m, 20m, false));
    }

    [Fact]
    public void CalcLineAmount_Gift_Is_Zero()
    {
        Assert.Equal(0m, OrderCalculator.CalcLineAmount(2, 32m, 0m, true));
    }

    [Fact]
    public void CalcLineAmount_Rounds_HalfUp()
    {
        // 3 × 10.005 = 30.015 → 30.02
        Assert.Equal(30.02m, OrderCalculator.CalcLineAmount(3, 10.005m, 0m, false));
    }

    // ---------- 称重 ----------

    [Fact]
    public void CalcWeightedAmount_Basic()
    {
        // 1.25kg × 68元/kg = 85.00
        Assert.Equal(85.00m, OrderCalculator.CalcWeightedAmount(1.25m, 68m));
    }

    [Fact]
    public void CalcWeightedAmount_Rounds_To_2()
    {
        // 0.333 × 100 = 33.30
        Assert.Equal(33.30m, OrderCalculator.CalcWeightedAmount(0.333m, 100m));
    }

    [Fact]
    public void CalcWeightedAmount_Zero_On_NonPositive()
    {
        Assert.Equal(0m, OrderCalculator.CalcWeightedAmount(0m, 68m));
        Assert.Equal(0m, OrderCalculator.CalcWeightedAmount(-1m, 68m));
        Assert.Equal(0m, OrderCalculator.CalcWeightedAmount(1m, 0m));
    }

    // ---------- 整单 ----------

    private static List<OrderLineInput> SampleLines() => new()
    {
        new OrderLineInput { Qty = 2, Price = 32m },                    // 64
        new OrderLineInput { Qty = 1, Price = 8m, LineDiscount = 2m },   // 6
    };

    [Fact]
    public void CalcTotals_Basic()
    {
        var t = OrderCalculator.CalcTotals(SampleLines());
        Assert.Equal(72.00m, t.OriginalTotal);
        Assert.Equal(70.00m, t.Subtotal);
        Assert.Equal(2.00m, t.LineDiscountTotal);
        Assert.Equal(70.00m, t.Payable);
    }

    [Fact]
    public void CalcTotals_OrderDiscountRate()
    {
        // 九折：70 × 0.9 = 63
        var t = OrderCalculator.CalcTotals(SampleLines(), orderDiscountRate: 0.9m);
        Assert.Equal(7.00m, t.OrderDiscount);
        Assert.Equal(63.00m, t.Payable);
    }

    [Fact]
    public void CalcTotals_InvalidRate_Treated_As_NoDiscount()
    {
        var t1 = OrderCalculator.CalcTotals(SampleLines(), orderDiscountRate: 0m);
        var t2 = OrderCalculator.CalcTotals(SampleLines(), orderDiscountRate: 1.5m);
        Assert.Equal(70.00m, t1.Payable);
        Assert.Equal(70.00m, t2.Payable);
    }

    [Fact]
    public void CalcTotals_WipeZero()
    {
        // 128.57 抹零 → 128.50
        var lines = new List<OrderLineInput> { new() { Qty = 1, Price = 128.57m } };
        var t = OrderCalculator.CalcTotals(lines, wipeZero: true);
        Assert.Equal(128.50m, t.Payable);
        Assert.Equal(0.07m, t.WipeZeroAmount);
    }

    [Fact]
    public void CalcTotals_WipeZero_Disabled_ByDefault()
    {
        var lines = new List<OrderLineInput> { new() { Qty = 1, Price = 128.57m } };
        var t = OrderCalculator.CalcTotals(lines, wipeZero: false);
        Assert.Equal(128.57m, t.Payable);
        Assert.Equal(0m, t.WipeZeroAmount);
    }

    [Fact]
    public void CalcTotals_Gift_Counts_Original_But_Zero_Payable()
    {
        var lines = new List<OrderLineInput>
        {
            new() { Qty = 1, Price = 50m, IsGift = true },  // 赠菜
            new() { Qty = 1, Price = 30m },
        };
        var t = OrderCalculator.CalcTotals(lines);
        Assert.Equal(80.00m, t.OriginalTotal); // 原价含赠菜
        Assert.Equal(30.00m, t.Subtotal);      // 小计不含赠菜
        Assert.Equal(30.00m, t.Payable);
    }

    [Fact]
    public void CalcTotals_Refunded_Excluded_From_Subtotal()
    {
        var lines = new List<OrderLineInput>
        {
            new() { Qty = 2, Price = 32m },                              // 64
            new() { Qty = 1, Price = 20m, IsRefunded = true },           // 退菜
        };
        var t = OrderCalculator.CalcTotals(lines);
        Assert.Equal(84.00m, t.OriginalTotal); // 原价保留（含退菜，对账用）
        Assert.Equal(64.00m, t.Subtotal);     // 小计扣除退菜
        Assert.Equal(64.00m, t.Payable);
    }

    [Fact]
    public void CalcTotals_Empty_Lines()
    {
        var t = OrderCalculator.CalcTotals(new List<OrderLineInput>());
        Assert.Equal(0m, t.Payable);
        Assert.Equal(0m, t.OriginalTotal);
    }

    // ---------- 找零 ----------

    [Fact]
    public void CalcChange_Basic()
    {
        Assert.Equal(28.00m, OrderCalculator.CalcChange(100m, 72m));
    }

    [Fact]
    public void CalcChange_Negative_When_Short()
    {
        Assert.Equal(-2.00m, OrderCalculator.CalcChange(70m, 72m));
    }

    // ---------- 抹零 ----------

    [Fact]
    public void WipeZero_Floors_To_Dime()
    {
        Assert.Equal(128.50m, OrderCalculator.WipeZero(128.57m));
        Assert.Equal(128.50m, OrderCalculator.WipeZero(128.50m));
        Assert.Equal(0m, OrderCalculator.WipeZero(0m));
    }
}
