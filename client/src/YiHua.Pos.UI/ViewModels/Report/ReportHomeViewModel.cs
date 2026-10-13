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

    // ---------- 参考端「营业报表」的补充口径 ----------

    /// <summary>退款金额（已退单的金额绝对值）。</summary>
    public decimal RefundAmount { get; private set; }

    /// <summary>退款笔数。</summary>
    public int RefundCount { get; private set; }

    /// <summary>未结账订单数。</summary>
    public int PendingOrderCount { get; private set; }

    /// <summary>未结账订单金额。</summary>
    public decimal PendingAmount { get; private set; }

    /// <summary>未结账订单中已支付的部分。</summary>
    public decimal PendingPaidAmount { get; private set; }

    /// <summary>未结账订单中待支付的部分。</summary>
    public decimal PendingUnpaidAmount { get; private set; }

    /// <summary>
    /// 净营业额。
    ///
    /// 直接取 Turnover —— 它已经是「已结账实收 - 退款」了
    /// （CalcTurnover 把冲正单当负数应付计入）。
    /// 不要再减一次 RefundAmount，那是重复扣减。
    /// RefundAmount 只是拿来单独显示「其中退了多少」。
    /// </summary>
    public decimal NetTurnover => Turnover;

    /// <summary>折后笔单价 = 净营业额 / 订单笔数。</summary>
    public decimal AvgTicketAfterDiscount =>
        OrderCount == 0 ? 0m : decimal.Round(NetTurnover / OrderCount, 2, MidpointRounding.AwayFromZero);

    /// <summary>当前时间范围描述（界面标题用）。</summary>
    public string RangeLabel { get; private set; } = "今日";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var start = ReportDate.Date;
            var end = start.AddDays(1);
            await LoadRangeAsync(start, end, "今日", ct);
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

    /// <summary>按任意时间范围加载（参考端的时间范围切换走这里）。</summary>
    public async Task LoadRangeAsync(DateTime start, DateTime end, string label, CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            ReportDate = start.Date;
            RangeLabel = label;

            var orders = await _dataSource.GetOrdersAsync(start, end, ct);

            Turnover = ReportCalculator.CalcTurnover(orders);
            OrderCount = ReportCalculator.CountValidOrders(orders);
            DiscountTotal = ReportCalculator.CalcDiscountTotal(orders);
            NetReceived = ReportCalculator.CalcNetReceived(orders);

            // 退款：已退单按绝对值统计，笔数单独算
            var refunded = orders.Where(o => o.Status == 2).ToList();
            RefundAmount = Math.Abs(refunded.Sum(o => o.AmountPay));
            RefundCount = refunded.Count;
            AvgTicket = ReportCalculator.CalcAvgTicket(NetTurnover, OrderCount);

            // 未结账订单统计（参考端单独一块）
            var pending = orders.Where(o => o.Status == 0 && !o.IsVoid).ToList();
            PendingOrderCount = pending.Count;
            PendingAmount = pending.Sum(o => o.AmountPay);
            PendingPaidAmount = pending.Sum(o => o.AmountReceived);
            PendingUnpaidAmount = Math.Max(0m, PendingAmount - PendingPaidAmount);

            // 同比：只有「今日」才有意义，其它范围跳过
            if (label == "今日")
            {
                var span = end - start;
                var prevOrders = await _dataSource.GetOrdersAsync(start - span, start, ct);
                TurnoverGrowth = ReportCalculator.CalcGrowthRate(
                    NetTurnover, ReportCalculator.CalcTurnover(prevOrders));
                OrderCountGrowth = ReportCalculator.CalcGrowthRate(
                    OrderCount, ReportCalculator.CountValidOrders(prevOrders));
            }
            else
            {
                TurnoverGrowth = 0m;
                OrderCountGrowth = 0m;
            }

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
