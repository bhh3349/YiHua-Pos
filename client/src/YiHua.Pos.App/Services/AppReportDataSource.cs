using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Report;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 报表数据源：委托给 <see cref="OrderDataProvider"/> 取订单，
/// 并把班次账实也接上。
///
/// 原 <c>LocalReportDataSource.GetShiftReportsAsync</c> 直接
/// <c>return Task.FromResult(new List&lt;ShiftReportDto&gt;())</c>
/// （注释写「班次数据由 T-309 提供，此处返回空，待联动」）。
/// T-309 已经做完并且 IShiftRepository 可用了，这里补上，
/// 否则老板在报表页看到的班次永远是空的。
/// </summary>
internal sealed class AppReportDataSource : IReportDataSource
{
    private readonly IOrderDataProvider _orders;
    private readonly IShiftRepository _shifts;

    public AppReportDataSource(IOrderDataProvider orders, IShiftRepository shifts)
    {
        _orders = orders;
        _shifts = shifts;
    }

    public async Task<List<ReportOrderDto>> GetOrdersAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var all = await _orders.GetAllAsync(ct).ConfigureAwait(false);
        return ReportCalculator.FilterByDate(all, start, end);
    }

    public async Task<List<ShiftReportDto>> GetShiftReportsAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var shifts = await _shifts.GetHistoryAsync(start, end, ct).ConfigureAwait(false);

        return shifts.Select(s => new ShiftReportDto
        {
            ShiftName = s.StartAt.ToString("MM-dd HH:mm") + " 班",
            OperatorName = s.OperatorName,
            OrderCount = s.OrderCount,
            // Shift 实体：CashShould = 应收，CashActual = 实收。
            // 报表里「应收/实收」指的是钱，不是营业额，所以用现金字段。
            ExpectedAmount = s.CashShould,
            ActualAmount = s.CashActual,
        }).ToList();
    }
}