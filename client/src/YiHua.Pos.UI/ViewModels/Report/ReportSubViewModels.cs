using YiHua.Pos.UI.Services.Report;

namespace YiHua.Pos.UI.ViewModels.Report;

/// <summary>
/// 时段分析 ViewModel。
/// </summary>
public class ReportHourlyViewModel
{
    private readonly IReportDataSource _dataSource;

    public ReportHourlyViewModel(IReportDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public DateTime ReportDate { get; set; } = DateTime.Today;
    public List<HourBucket> Buckets { get; private set; } = new();

    /// <summary>高峰时段描述（如"11-13点"）。</summary>
    public string PeakLabel { get; private set; } = string.Empty;

    public decimal MaxTurnover => Buckets.Count > 0 ? Buckets.Max(b => b.Turnover) : 0m;

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var orders = await _dataSource.GetOrdersAsync(ReportDate.Date, ReportDate.Date.AddDays(1), ct);
            Buckets = ReportCalculator.BucketByHour(orders);
            PeakLabel = BuildPeakLabel();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string BuildPeakLabel()
    {
        var peaks = Buckets.Where(b => b.IsPeak).OrderBy(b => b.Hour).ToList();
        if (peaks.Count == 0) return "暂无高峰时段";
        return $"高峰时段：{peaks.First().Hour}点 - {peaks.Last().Hour + 1}点";
    }
}

/// <summary>
/// 菜品排行 ViewModel。
/// </summary>
public class ReportProductRankViewModel
{
    private readonly IReportDataSource _dataSource;

    public ReportProductRankViewModel(IReportDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public DateTime Start { get; set; } = DateTime.Today;
    public DateTime End { get; set; } = DateTime.Today.AddDays(1);

    /// <summary>true=按销售额，false=按销量。</summary>
    public bool SortByAmount { get; set; } = true;

    public List<RankItem> Items { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var orders = await _dataSource.GetOrdersAsync(Start, End, ct);
            Items = ReportCalculator.RankProducts(orders, SortByAmount, topN: 20);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>
/// 桌台统计 ViewModel。
/// </summary>
public class ReportDeskRankViewModel
{
    private readonly IReportDataSource _dataSource;

    public ReportDeskRankViewModel(IReportDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public DateTime Start { get; set; } = DateTime.Today;
    public DateTime End { get; set; } = DateTime.Today.AddDays(1);

    public List<RankItem> Items { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var orders = await _dataSource.GetOrdersAsync(Start, End, ct);
            Items = ReportCalculator.RankDesks(orders);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>桌台平均客单价。</summary>
    public decimal GetAvg(RankItem item) => ReportCalculator.CalcDeskAvg(item);
}

/// <summary>
/// 班次账实 ViewModel（只读）。
/// </summary>
public class ReportShiftViewModel
{
    private readonly IReportDataSource _dataSource;

    public ReportShiftViewModel(IReportDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public DateTime Start { get; set; } = DateTime.Today;
    public DateTime End { get; set; } = DateTime.Today.AddDays(1);

    public List<ShiftReportDto> Shifts { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            Shifts = await _dataSource.GetShiftReportsAsync(Start, End, ct);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>
/// 下钻明细 ViewModel。
/// </summary>
public class ReportDetailViewModel
{
    public string Title { get; set; } = string.Empty;
    public List<ReportOrderDto> Orders { get; } = new();

    public decimal Turnover => ReportCalculator.CalcTurnover(Orders);
    public int OrderCount => ReportCalculator.CountValidOrders(Orders);
}
