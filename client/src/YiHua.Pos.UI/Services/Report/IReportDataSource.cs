namespace YiHua.Pos.UI.Services.Report;

/// <summary>
/// 报表数据源接口（本地/服务端可切换）。
/// 本期实现 LocalReportDataSource（本地 SQLite 聚合）；
/// 长期需要服务端报表接口（数据量大，不应全量拉到客户端）。
/// </summary>
public interface IReportDataSource
{
    /// <summary>
    /// 按日期范围获取订单（归属时间在 [start, end) 内）。
    /// 本地实现：SQLite 按索引查询；服务端实现：调聚合接口。
    /// </summary>
    Task<List<ReportOrderDto>> GetOrdersAsync(DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// 获取班次账实（T-309 联动，只读）。
    /// </summary>
    Task<List<ShiftReportDto>> GetShiftReportsAsync(DateTime start, DateTime end, CancellationToken ct = default);
}

/// <summary>班次账实 DTO。</summary>
public class ShiftReportDto
{
    public string ShiftName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal ExpectedAmount { get; set; } // 应收
    public decimal ActualAmount { get; set; }   // 实点
    public decimal Difference => ActualAmount - ExpectedAmount;
}

/// <summary>
/// 本地报表数据源：基于订单仓储的内存聚合。
/// 注意：UI 层无法引用 Data/Domain（csproj 冻结），
/// 此处依赖 IOrderDataProvider（由 App 层用 IOrderRepository 实现）。
/// 大范围查询应分页，调用方控制。
/// </summary>
public class LocalReportDataSource : IReportDataSource
{
    private readonly IOrderDataProvider _orderProvider;

    public LocalReportDataSource(IOrderDataProvider orderProvider)
    {
        _orderProvider = orderProvider;
    }

    public async Task<List<ReportOrderDto>> GetOrdersAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var all = await _orderProvider.GetAllAsync(ct);
        return ReportCalculator.FilterByDate(all, start, end);
    }

    public Task<List<ShiftReportDto>> GetShiftReportsAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        // 班次数据由 T-309 提供，此处返回空，待联动
        return Task.FromResult(new List<ShiftReportDto>());
    }
}

/// <summary>
/// 订单数据提供者（UI 层契约，App 层用 IOrderRepository 实现）。
/// </summary>
public interface IOrderDataProvider
{
    Task<List<ReportOrderDto>> GetAllAsync(CancellationToken ct = default);
}
