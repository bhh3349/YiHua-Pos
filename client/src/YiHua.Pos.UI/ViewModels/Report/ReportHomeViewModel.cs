using YiHua.Pos.UI.Services.Report;

namespace YiHua.Pos.UI.ViewModels.Report;

/// <summary>
/// 今日概览 ViewModel。
/// </summary>
public class ReportHomeViewModel
{
    private readonly IReportDataSource _dataSource;

    public ReportHomeViewModel(IReportDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public DateTime ReportDate { get; set; } = DateTime.Today;

    public decimal Turnover { get; private set; }
    public int OrderCount { get; private set; }
    public decimal AvgTicket { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal NetReceived { get; private set; }

    public decimal TurnoverGrowth { get; private set; }
    public decimal OrderCountGrowth { get; private set; }

    public List<PayTypeShareDto> PayTypeBreakdown { get; private set; } = new();
    public List<OrderTypeShareDto> OrderTypeBreakdown { get; private set; } = new();

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsEmpty => !IsLoading && ErrorMessage == null && OrderCount == 0;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var start = ReportDate.Date;
            var end = start.AddDays(1);
            var orders = await _dataSource.GetOrdersAsync(start, end, ct);

            Turnover = ReportCalculator.CalcTurnover(orders);
            OrderCount = ReportCalculator.CountValidOrders(orders);
            AvgTicket = ReportCalculator.CalcAvgTicket(Turnover, OrderCount);
            DiscountTotal = ReportCalculator.CalcDiscountTotal(orders);
            NetReceived = ReportCalculator.CalcNetReceived(orders);

            // 较昨日同比
            var prevOrders = await _dataSource.GetOrdersAsync(start.AddDays(-1), start, ct);
            var prevTurnover = ReportCalculator.CalcTurnover(prevOrders);
            var prevCount = ReportCalculator.CountValidOrders(prevOrders);
            TurnoverGrowth = ReportCalculator.CalcGrowthRate(Turnover, prevTurnover);
            OrderCountGrowth = ReportCalculator.CalcGrowthRate(OrderCount, prevCount);

            PayTypeBreakdown = ReportCalculator.PayTypeBreakdown(orders)
                .Select(x => new PayTypeShareDto
                {
                    PayType = x.PayType,
                    Name = x.Name,
                    Amount = x.Amount,
                    Share = x.Share,
                }).ToList();
            OrderTypeBreakdown = BuildOrderTypeBreakdown(orders);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载报表失败：{ex.Message}，点击重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static List<OrderTypeShareDto> BuildOrderTypeBreakdown(List<ReportOrderDto> orders)
    {
        return orders
            .Where(o => !o.IsVoid)
            .GroupBy(o => o.OrderType)
            .Select(g => new OrderTypeShareDto
            {
                OrderType = g.Key,
                Name = GetOrderTypeName(g.Key),
                Amount = ReportCalculator.CalcTurnover(g.ToList()),
            })
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    private static string GetOrderTypeName(int type) => type switch
    {
        1 => "堂食",
        2 => "外带",
        3 => "外卖",
        4 => "自提",
        _ => "其他",
    };
}

/// <summary>
/// 日期范围 ViewModel。
/// </summary>
public class ReportRangeViewModel
{
    public const int MaxDays = 90;

    public DateTime Start { get; set; } = DateTime.Today;
    public DateTime End { get; set; } = DateTime.Today.AddDays(1);

    public ReportRangePreset Preset { get; set; } = ReportRangePreset.Today;

    /// <summary>
    /// 应用预设（可测试）。
    /// </summary>
    public void ApplyPreset(ReportRangePreset preset)
    {
        Preset = preset;
        var today = DateTime.Today;
        (Start, End) = preset switch
        {
            ReportRangePreset.Today => (today, today.AddDays(1)),
            ReportRangePreset.Yesterday => (today.AddDays(-1), today),
            ReportRangePreset.ThisWeek => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(1)),
            ReportRangePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1), today.AddDays(1)),
            _ => (Start, End),
        };
    }

    /// <summary>
    /// 校验范围（可测试）。返回 null 表示合法。
    /// </summary>
    public string? Validate()
    {
        if (End <= Start)
            return "结束日期必须晚于开始日期";
        if ((End - Start).TotalDays > MaxDays)
            return $"日期范围不能超过 {MaxDays} 天，请缩小范围";
        return null;
    }
}

public enum ReportRangePreset
{
    Today,
    Yesterday,
    ThisWeek,
    ThisMonth,
    Custom,
}

/// <summary>支付方式占比 DTO。</summary>
public class PayTypeShareDto
{
    public int PayType { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Share { get; set; }
}

/// <summary>订单类型占比 DTO。</summary>
public class OrderTypeShareDto
{
    public int OrderType { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
