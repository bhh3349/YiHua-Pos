using YiHua.Pos.UI.Services.Report;
using Xunit;

namespace YiHua.Pos.Tests.UI.Report;

/// <summary>
/// 边界测试：分母为0、空数据、跨天、负数。
/// </summary>
public class ReportBoundaryTests
{
    [Fact]
    public void EmptyOrders_AllCalculations_ReturnZero()
    {
        var empty = new List<ReportOrderDto>();
        Assert.Equal(0m, ReportCalculator.CalcTurnover(empty));
        Assert.Equal(0m, ReportCalculator.CalcDiscountTotal(empty));
        Assert.Equal(0m, ReportCalculator.CalcNetReceived(empty));
        Assert.Equal(0, ReportCalculator.CountValidOrders(empty));
        Assert.Empty(ReportCalculator.RankProducts(empty));
        Assert.Empty(ReportCalculator.RankDesks(empty));
        Assert.Empty(ReportCalculator.PayTypeBreakdown(empty));
    }

    [Fact]
    public void AllVoidOrders_TurnoverZero()
    {
        var orders = new[]
        {
            new ReportOrderDto { AmountPay = 100m, Status = 3 },
            new ReportOrderDto { AmountPay = 200m, Status = 3 },
        };
        Assert.Equal(0m, ReportCalculator.CalcTurnover(orders));
        Assert.Equal(0, ReportCalculator.CountValidOrders(orders));
    }

    [Fact]
    public void CrossDayOrder_AttributedToSettleTime()
    {
        // 23:50 下单，次日 00:10 结账 → 归属次日
        var order = new ReportOrderDto
        {
            AmountPay = 100m,
            Status = 1,
            CreatedAt = new DateTime(2026, 10, 12, 23, 50, 0),
            SettleTime = new DateTime(2026, 10, 13, 0, 10, 0),
        };
        Assert.Equal(new DateTime(2026, 10, 13, 0, 10, 0), order.AttributedTime);

        var filtered = ReportCalculator.FilterByDate(
            new[] { order },
            new DateTime(2026, 10, 13, 0, 0, 0),
            new DateTime(2026, 10, 14, 0, 0, 0));
        Assert.Single(filtered);
    }

    [Fact]
    public void CrossDayOrder_NoSettleTime_FallsBackToCreatedAt()
    {
        var order = new ReportOrderDto
        {
            AmountPay = 100m,
            Status = 0, // 待结账，无结账时间
            CreatedAt = new DateTime(2026, 10, 12, 23, 50, 0),
            SettleTime = null,
        };
        Assert.Equal(new DateTime(2026, 10, 12, 23, 50, 0), order.AttributedTime);
    }

    [Fact]
    public void NegativeRefund_HandledCorrectly()
    {
        // 退菜金额大于应付的异常数据，不崩溃
        var orders = new[] { new ReportOrderDto { AmountPay = 50m, AmountRefund = 60m, Status = 1 } };
        Assert.Equal(-10m, ReportCalculator.CalcTurnover(orders));
    }

    [Fact]
    public void Share_AllZero_DoesNotReturnNaN()
    {
        var share = ReportCalculator.CalcShare(0m, 0m);
        Assert.Equal(0m, share);
        // decimal 类型本身无 NaN，断言返回 0 即证明边界处理正确
    }

    [Fact]
    public void GrowthRate_NegativePrevious_Handled()
    {
        // 上期为负（冲正多）的边界
        var rate = ReportCalculator.CalcGrowthRate(100m, -50m);
        Assert.Equal(3m, rate); // (100 - (-50)) / 50 = 3
    }
}

/// <summary>
/// 计算规则测试：退菜/冲正/抹零/作废单。
/// </summary>
public class ReportRulesTests
{
    [Fact]
    public void Refund_ReducesTurnover()
    {
        // 退菜：已退金额从营业额中减去
        var order = new ReportOrderDto
        {
            AmountPay = 100m,
            AmountRefund = 30m, // 退了 30
            Status = 1,
        };
        Assert.Equal(70m, ReportCalculator.CalcTurnover(new[] { order }));
    }

    [Fact]
    public void Discount_IncludesRounding()
    {
        // 抹零：抹掉的部分计入折扣
        var order = new ReportOrderDto
        {
            AmountTotal = 74.57m,
            AmountDiscount = 0.57m, // 抹零到元
            AmountPay = 74m,
            Status = 1,
        };
        Assert.Equal(0.57m, ReportCalculator.CalcDiscountTotal(new[] { order }));
        Assert.Equal(74m, ReportCalculator.CalcTurnover(new[] { order }));
    }

    [Fact]
    public void VoidOrder_ExcludedFromAll()
    {
        var order = new ReportOrderDto
        {
            AmountPay = 100m,
            AmountDiscount = 10m,
            AmountReceived = 90m,
            Status = 3, // 作废
        };
        var orders = new[] { order };
        Assert.Equal(0m, ReportCalculator.CalcTurnover(orders));
        Assert.Equal(0m, ReportCalculator.CalcDiscountTotal(orders));
        Assert.Equal(0m, ReportCalculator.CalcNetReceived(orders));
    }

    [Fact]
    public void RefundedItem_ExcludedFromRanking()
    {
        var order = new ReportOrderDto { AmountPay = 100m, Status = 1 };
        order.Items.Add(new ReportOrderItemDto
        {
            ProductId = 1, ProductName = "牛肉面", Qty = 2, Price = 18m, Amount = 36m,
            IsRefunded = true, // 已退
        });
        order.Items.Add(new ReportOrderItemDto
        {
            ProductId = 2, ProductName = "宫保鸡丁", Qty = 1, Price = 38m, Amount = 38m,
        });

        var ranking = ReportCalculator.RankProducts(new[] { order });
        Assert.Single(ranking); // 只有宫保鸡丁
        Assert.Equal("宫保鸡丁", ranking[0].Name);
    }

    [Fact]
    public void GiftItem_CountsQuantityNotAmount()
    {
        var order = new ReportOrderDto { AmountPay = 38m, Status = 1 };
        order.Items.Add(new ReportOrderItemDto
        {
            ProductId = 1, ProductName = "牛肉面", Qty = 1, Price = 18m, Amount = 0m,
            IsGift = true, // 赠菜
        });

        var ranking = ReportCalculator.RankProducts(new[] { order });
        Assert.Single(ranking);
        Assert.Equal(1m, ranking[0].Quantity); // 计销量
        Assert.Equal(0m, ranking[0].Amount);   // 不计金额
    }
}
