using YiHua.Pos.UI.Services.Report;
using YiHua.Pos.UI.ViewModels.Report;
using Xunit;

namespace YiHua.Pos.Tests.UI.Report;

/// <summary>
/// 报表口径测试。
///
/// 守的是「同一个词在不同页面必须是同一个意思」：
///   净营业额 = 已结账实收 - 退款
///   折后笔单价 = 净营业额 / 订单笔数
///   未结账订单不计入营业额
/// 报表口径飘了，老板按这个数做的决策就是错的。
/// </summary>
public class ReportMetricTests
{
    private sealed class FakeDataSource : IReportDataSource
    {
        public List<ReportOrderDto> Orders { get; } = new();

        public Task<List<ReportOrderDto>> GetOrdersAsync(DateTime start, DateTime end, CancellationToken ct = default)
            => Task.FromResult(Orders.Where(o => o.AttributedTime >= start && o.AttributedTime < end).ToList());

        public Task<List<ShiftReportDto>> GetShiftReportsAsync(DateTime start, DateTime end, CancellationToken ct = default)
            => Task.FromResult(new List<ShiftReportDto>());
    }

    private static ReportOrderDto Settled(decimal pay, DateTime when, int payType = 1)
    {
        var o = new ReportOrderDto
        {
            Id = Random.Shared.Next(1, 999999),
            OrderNo = "T" + Guid.NewGuid().ToString("N")[..6],
            Status = 1,
            AmountTotal = pay,
            AmountPay = pay,
            AmountReceived = pay,
            SettleTime = when,
            CreatedAt = when,
        };
        o.Payments.Add(new ReportPaymentDto { PayType = payType, Amount = pay });
        return o;
    }

    private static ReportOrderDto Pending(decimal pay, decimal received, DateTime when)
        => new()
        {
            Id = Random.Shared.Next(1, 999999),
            OrderNo = "P" + Guid.NewGuid().ToString("N")[..6],
            Status = 0,
            AmountTotal = pay,
            AmountPay = pay,
            AmountReceived = received,
            CreatedAt = when,
        };

    private static ReportOrderDto Refunded(decimal pay, DateTime when)
        => new()
        {
            Id = Random.Shared.Next(1, 999999),
            OrderNo = "R" + Guid.NewGuid().ToString("N")[..6],
            Status = 2,
            AmountTotal = pay,
            AmountPay = -pay,
            CreatedAt = when,
        };

    private static async Task<ReportHomeViewModel> LoadAsync(FakeDataSource src, DateTime start, DateTime end, string label)
    {
        var vm = new ReportHomeViewModel(src);
        await vm.LoadRangeAsync(start, end, label);
        return vm;
    }

    [Fact]
    public async Task 净营业额_等于已结账实收()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddHours(10)));
        src.Orders.Add(Settled(50m, DateTime.Today.AddHours(12)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(150m, vm.NetTurnover);
        Assert.Equal(2, vm.OrderCount);
    }

    [Fact]
    public async Task 净营业额_要扣掉退款()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddHours(10)));
        src.Orders.Add(Refunded(30m, DateTime.Today.AddHours(11)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        // 100 实收 - 30 退款 = 70
        Assert.Equal(70m, vm.NetTurnover);
        Assert.Equal(30m, vm.RefundAmount);
        Assert.Equal(1, vm.RefundCount);
    }

    [Fact]
    public async Task 未结账订单_不计入营业额()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddHours(10)));
        src.Orders.Add(Pending(80m, 0m, DateTime.Today.AddHours(11)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(100m, vm.NetTurnover);
        Assert.Equal(1, vm.OrderCount);
        Assert.Equal(1, vm.PendingOrderCount);
        Assert.Equal(80m, vm.PendingAmount);
    }

    [Fact]
    public async Task 未结账统计_已支付与待支付分开()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Pending(80m, 30m, DateTime.Today.AddHours(11)));
        src.Orders.Add(Pending(20m, 20m, DateTime.Today.AddHours(12)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(2, vm.PendingOrderCount);
        Assert.Equal(100m, vm.PendingAmount);
        Assert.Equal(50m, vm.PendingPaidAmount);
        Assert.Equal(50m, vm.PendingUnpaidAmount);
    }

    [Fact]
    public async Task 折后笔单价_按净营业额算()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddHours(10)));
        src.Orders.Add(Settled(50m, DateTime.Today.AddHours(11)));
        src.Orders.Add(Refunded(30m, DateTime.Today.AddHours(12)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        // 净营业额 120 / 2 笔 = 60
        Assert.Equal(120m, vm.NetTurnover);
        Assert.Equal(2, vm.OrderCount);
        Assert.Equal(60m, vm.AvgTicketAfterDiscount);
    }

    [Fact]
    public async Task 没有订单时_笔单价为零不除零()
    {
        var vm = await LoadAsync(new FakeDataSource(), DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(0m, vm.NetTurnover);
        Assert.Equal(0m, vm.AvgTicketAfterDiscount);
        Assert.Equal(0, vm.OrderCount);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task 时间范围外的订单不计入()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddDays(-2).AddHours(10)));
        src.Orders.Add(Settled(50m, DateTime.Today.AddHours(10)));

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(50m, vm.NetTurnover);
        Assert.Equal(1, vm.OrderCount);
    }

    [Fact]
    public async Task 作废订单不计入营业额()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddHours(10)));
        src.Orders.Add(new ReportOrderDto
        {
            Id = 999, Status = 3, AmountPay = 999m,
            CreatedAt = DateTime.Today.AddHours(11),
        });

        var vm = await LoadAsync(src, DateTime.Today, DateTime.Today.AddDays(1), "今日");

        Assert.Equal(100m, vm.NetTurnover);
        Assert.Equal(1, vm.OrderCount);
    }

    [Fact]
    public async Task 时间范围标签会记下来()
    {
        var src = new FakeDataSource();

        var vm = await LoadAsync(src, DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1), "近 7 天");

        Assert.Equal("近 7 天", vm.RangeLabel);
    }

    [Fact]
    public async Task 非今日范围不算同比()
    {
        var src = new FakeDataSource();
        src.Orders.Add(Settled(100m, DateTime.Today.AddDays(-1).AddHours(10)));
        src.Orders.Add(Settled(50m, DateTime.Today.AddDays(-2).AddHours(10)));

        var vm = await LoadAsync(src, DateTime.Today.AddDays(-3), DateTime.Today.AddDays(1), "近 7 天");

        // 范围不是「今日」时不显示同比，避免拿不相邻的两段做对比
        Assert.Equal(0m, vm.TurnoverGrowth);
        Assert.Equal(0m, vm.OrderCountGrowth);
    }

    [Fact]
    public void 星期几_中文()
    {
        // 2026-10-14 是周三
        Assert.Equal("周三", YiHua.Pos.UI.Views.Report.ReportHomeView.GetWeekday(new DateTime(2026, 10, 14)));
        Assert.Equal("周日", YiHua.Pos.UI.Views.Report.ReportHomeView.GetWeekday(new DateTime(2026, 10, 18)));
    }
}