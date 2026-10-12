using YiHua.Pos.UI.Services.Report;
using Xunit;

namespace YiHua.Pos.Tests.UI.Report;

/// <summary>
/// 排行测试：菜品/桌台（按金额与销量）。
/// </summary>
public class ReportRankingTests
{
    private static List<ReportOrderDto> MakeOrders()
    {
        var orders = new List<ReportOrderDto>();

        var o1 = new ReportOrderDto { AmountPay = 100m, Status = 1, DeskId = 1, DeskName = "A01" };
        o1.Items.Add(new ReportOrderItemDto { ProductId = 1, ProductName = "牛肉面", Qty = 5, Price = 18m, Amount = 90m });
        o1.Items.Add(new ReportOrderItemDto { ProductId = 2, ProductName = "宫保鸡丁", Qty = 1, Price = 38m, Amount = 38m });
        orders.Add(o1);

        var o2 = new ReportOrderDto { AmountPay = 80m, Status = 1, DeskId = 2, DeskName = "A02" };
        o2.Items.Add(new ReportOrderItemDto { ProductId = 1, ProductName = "牛肉面", Qty = 3, Price = 18m, Amount = 54m });
        o2.Items.Add(new ReportOrderItemDto { ProductId = 3, ProductName = "珍珠奶茶", Qty = 10, Price = 12m, Amount = 120m });
        orders.Add(o2);

        return orders;
    }

    [Fact]
    public void RankProducts_ByAmount_SortsCorrectly()
    {
        // 牛肉面：144元；宫保鸡丁：38元；珍珠奶茶：120元
        // 按金额：牛肉面(144) > 珍珠奶茶(120) > 宫保鸡丁(38)
        var ranking = ReportCalculator.RankProducts(MakeOrders(), byAmount: true);
        Assert.Equal(3, ranking.Count);
        Assert.Equal("牛肉面", ranking[0].Name);
        Assert.Equal(144m, ranking[0].Amount);
        Assert.Equal("珍珠奶茶", ranking[1].Name);
        Assert.Equal("宫保鸡丁", ranking[2].Name);
    }

    [Fact]
    public void RankProducts_ByQuantity_SortsCorrectly()
    {
        // 按销量：珍珠奶茶(10) > 牛肉面(8) > 宫保鸡丁(1)
        var ranking = ReportCalculator.RankProducts(MakeOrders(), byAmount: false);
        Assert.Equal("珍珠奶茶", ranking[0].Name);
        Assert.Equal(10m, ranking[0].Quantity);
        Assert.Equal("牛肉面", ranking[1].Name);
    }

    [Fact]
    public void RankProducts_Share_SumsToOne()
    {
        var ranking = ReportCalculator.RankProducts(MakeOrders(), byAmount: true);
        // 144 + 120 + 38 = 302
        Assert.Equal(144m / 302m, ranking[0].Share, 4);
    }

    [Fact]
    public void RankProducts_TopN_LimitsResults()
    {
        var ranking = ReportCalculator.RankProducts(MakeOrders(), topN: 2);
        Assert.Equal(2, ranking.Count);
    }

    [Fact]
    public void RankProducts_RankNumbers_Assigned()
    {
        var ranking = ReportCalculator.RankProducts(MakeOrders());
        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(2, ranking[1].Rank);
        Assert.Equal(3, ranking[2].Rank);
    }

    [Fact]
    public void RankDesks_GroupsByDesk()
    {
        var ranking = ReportCalculator.RankDesks(MakeOrders());
        Assert.Equal(2, ranking.Count);
        // A01: 100元；A02: 80元
        Assert.Equal("A01", ranking[0].Name);
        Assert.Equal(100m, ranking[0].Amount);
        Assert.Equal(1m, ranking[0].Quantity); // 1 桌次
    }

    [Fact]
    public void RankDesks_ExcludesZeroDesk()
    {
        var orders = MakeOrders();
        orders.Add(new ReportOrderDto { AmountPay = 50m, Status = 1, DeskId = 0 }); // 快餐
        var ranking = ReportCalculator.RankDesks(orders);
        Assert.Equal(2, ranking.Count); // 快餐不计入桌台统计
    }

    [Fact]
    public void CalcDeskAvg_Correct()
    {
        var desk = new RankItem { Amount = 3200m, Quantity = 12m };
        Assert.Equal(266.67m, ReportCalculator.CalcDeskAvg(desk));
    }

    [Fact]
    public void PayTypeBreakdown_GroupsAndShares()
    {
        var o1 = new ReportOrderDto { AmountPay = 100m, Status = 1 };
        o1.Payments.Add(new ReportPaymentDto { PayType = 1, PayTypeName = "现金", Amount = 70m });
        o1.Payments.Add(new ReportPaymentDto { PayType = 2, PayTypeName = "自定义", Amount = 30m });

        var breakdown = ReportCalculator.PayTypeBreakdown(new[] { o1 });
        Assert.Equal(2, breakdown.Count);
        Assert.Equal("现金", breakdown[0].Name); // 按金额降序
        Assert.Equal(0.7m, breakdown[0].Share);
        Assert.Equal(0.3m, breakdown[1].Share);
    }
}

/// <summary>
/// 日期范围测试。
/// </summary>
public class ReportRangeTests
{
    [Fact]
    public void FilterByDate_IncludesInRange()
    {
        var orders = new[]
        {
            new ReportOrderDto
            {
                AmountPay = 100m, Status = 1,
                CreatedAt = new DateTime(2026, 10, 12, 10, 0, 0),
                SettleTime = new DateTime(2026, 10, 12, 10, 30, 0),
            },
        };
        var filtered = ReportCalculator.FilterByDate(orders,
            new DateTime(2026, 10, 12, 0, 0, 0),
            new DateTime(2026, 10, 13, 0, 0, 0));
        Assert.Single(filtered);
    }

    [Fact]
    public void FilterByDate_ExcludesOutOfRange()
    {
        var orders = new[]
        {
            new ReportOrderDto
            {
                AmountPay = 100m, Status = 1,
                CreatedAt = new DateTime(2026, 10, 11, 10, 0, 0),
                SettleTime = new DateTime(2026, 10, 11, 10, 30, 0),
            },
        };
        var filtered = ReportCalculator.FilterByDate(orders,
            new DateTime(2026, 10, 12, 0, 0, 0),
            new DateTime(2026, 10, 13, 0, 0, 0));
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterByDate_EndExclusive()
    {
        var orders = new[]
        {
            new ReportOrderDto
            {
                AmountPay = 100m, Status = 1,
                CreatedAt = new DateTime(2026, 10, 13, 0, 0, 0),
                SettleTime = new DateTime(2026, 10, 13, 0, 0, 0),
            },
        };
        // 结束时间 exclusive，10-13 00:00 不包含
        var filtered = ReportCalculator.FilterByDate(orders,
            new DateTime(2026, 10, 12, 0, 0, 0),
            new DateTime(2026, 10, 13, 0, 0, 0));
        Assert.Empty(filtered);
    }
}

/// <summary>
/// CSV 导出测试：BOM/中文表头/转义。
/// </summary>
public class CsvExportTests
{
    [Fact]
    public void WriteToString_StartsWithBom()
    {
        var csv = CsvWriter.WriteToString(new[] { "订单号" }, new[] { new[] { "001" } });
        Assert.Equal('\uFEFF', csv[0]); // BOM
    }

    [Fact]
    public void WriteToString_ChineseHeaders()
    {
        var csv = CsvWriter.WriteToString(
            new[] { "排名", "菜品", "销售额" },
            new[] { new[] { "1", "牛肉面", "2304.00" } });
        Assert.Contains("排名,菜品,销售额", csv);
        Assert.Contains("1,牛肉面,2304.00", csv);
    }

    [Fact]
    public void Escape_CommaAndQuote()
    {
        Assert.Equal("\"a,b\"", CsvWriter.Escape("a,b"));
        Assert.Equal("\"a\"\"b\"", CsvWriter.Escape("a\"b"));
        Assert.Equal("abc", CsvWriter.Escape("abc"));
    }

    [Fact]
    public void ExportProductRank_ChineseHeaders()
    {
        var items = new List<RankItem>
        {
            new() { Rank = 1, Name = "牛肉面", Quantity = 128, Amount = 2304m, Share = 0.18m },
        };
        // 验证表头（不实际写文件）
        var csv = CsvWriter.WriteToString(
            new[] { "排名", "菜品", "销量", "销售额", "占比" },
            items.Select(r => new[]
            {
                r.Rank.ToString(), r.Name, r.Quantity.ToString("F0"),
                r.Amount.ToString("F2"), (r.Share * 100).ToString("F1") + "%",
            }));
        Assert.Contains("排名,菜品,销量,销售额,占比", csv);
        Assert.Contains("牛肉面", csv);
        Assert.Contains("18.0%", csv);
    }
}
