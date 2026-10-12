using YiHua.Pos.UI.Services.Report;
using Xunit;

namespace YiHua.Pos.Tests.UI.Report;

/// <summary>
/// 报表计算器测试：营业额/客单价/占比/时段分桶。
/// </summary>
public class ReportCalculatorTests
{
    private static ReportOrderDto MakeOrder(
        decimal pay, decimal refund = 0, int status = 1,
        DateTime? settleTime = null, decimal discount = 0, decimal received = 0)
        => new()
        {
            AmountPay = pay,
            AmountRefund = refund,
            Status = status,
            SettleTime = settleTime ?? new DateTime(2026, 10, 12, 12, 0, 0),
            CreatedAt = new DateTime(2026, 10, 12, 11, 0, 0),
            AmountDiscount = discount,
            AmountReceived = received,
        };

    [Fact]
    public void CalcTurnover_SumsPayMinusRefund()
    {
        var orders = new[]
        {
            MakeOrder(100m, refund: 10m),
            MakeOrder(50m),
        };
        Assert.Equal(140m, ReportCalculator.CalcTurnover(orders));
    }

    [Fact]
    public void CalcTurnover_ExcludesVoid()
    {
        var orders = new[]
        {
            MakeOrder(100m),
            MakeOrder(200m, status: 3), // 作废
        };
        Assert.Equal(100m, ReportCalculator.CalcTurnover(orders));
    }

    [Fact]
    public void CalcTurnover_NegativeOrder_Counts()
    {
        // 冲正单（负数）计入
        var orders = new[] { MakeOrder(100m), MakeOrder(-20m) };
        Assert.Equal(80m, ReportCalculator.CalcTurnover(orders));
    }

    [Fact]
    public void CalcAvgTicket_UsesOrderCount()
    {
        // 客单价分母用订单数，不用件数
        Assert.Equal(70m, ReportCalculator.CalcAvgTicket(140m, 2));
    }

    [Fact]
    public void CalcAvgTicket_ZeroOrders_ReturnsZero()
    {
        Assert.Equal(0m, ReportCalculator.CalcAvgTicket(100m, 0));
    }

    [Fact]
    public void CalcDiscountTotal_SumsDiscounts()
    {
        var orders = new[] { MakeOrder(100m, discount: 10m), MakeOrder(50m, discount: 5m) };
        Assert.Equal(15m, ReportCalculator.CalcDiscountTotal(orders));
    }

    [Fact]
    public void CalcNetReceived_SumsReceived()
    {
        var orders = new[] { MakeOrder(100m, received: 100m), MakeOrder(50m, received: 60m) };
        Assert.Equal(160m, ReportCalculator.CalcNetReceived(orders));
    }

    [Fact]
    public void CalcGrowthRate_Correct()
    {
        // (12860 - 11880) / 11880 = 8.25%
        Assert.Equal(0.0825m, ReportCalculator.CalcGrowthRate(12860m, 11880m));
    }

    [Fact]
    public void CalcGrowthRate_PreviousZero_ReturnsZero()
    {
        Assert.Equal(0m, ReportCalculator.CalcGrowthRate(100m, 0m));
    }

    [Fact]
    public void CalcShare_Normal()
    {
        Assert.Equal(0.25m, ReportCalculator.CalcShare(25m, 100m));
    }

    [Fact]
    public void CalcShare_ZeroDenominator_ReturnsZero()
    {
        // 边界：分母为 0 返回 0，不抛异常、不返回 NaN
        Assert.Equal(0m, ReportCalculator.CalcShare(25m, 0m));
        Assert.Equal(0m, ReportCalculator.CalcShare(0m, 0m));
    }

    [Fact]
    public void BucketByHour_GroupsCorrectly()
    {
        var orders = new[]
        {
            MakeOrder(100m, settleTime: new DateTime(2026, 10, 12, 12, 30, 0)),
            MakeOrder(50m, settleTime: new DateTime(2026, 10, 12, 12, 45, 0)),
            MakeOrder(200m, settleTime: new DateTime(2026, 10, 12, 18, 0, 0)),
        };
        var buckets = ReportCalculator.BucketByHour(orders);
        Assert.Equal(24, buckets.Count);
        Assert.Equal(150m, buckets[12].Turnover);
        Assert.Equal(2, buckets[12].OrderCount);
        Assert.Equal(200m, buckets[18].Turnover);
    }

    [Fact]
    public void BucketByHour_MarksPeak()
    {
        var orders = new[]
        {
            MakeOrder(1000m, settleTime: new DateTime(2026, 10, 12, 12, 0, 0)),
            MakeOrder(100m, settleTime: new DateTime(2026, 10, 12, 9, 0, 0)),
        };
        var buckets = ReportCalculator.BucketByHour(orders);
        Assert.True(buckets[12].IsPeak); // 12点最高
    }

    [Fact]
    public void BucketByHour_EmptyData_NoPeak()
    {
        var buckets = ReportCalculator.BucketByHour(new List<ReportOrderDto>());
        Assert.All(buckets, b => Assert.False(b.IsPeak));
        Assert.All(buckets, b => Assert.Equal(0m, b.Turnover));
    }
}
