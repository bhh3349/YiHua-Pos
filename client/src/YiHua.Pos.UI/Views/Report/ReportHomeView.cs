using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using YiHua.Pos.UI.ViewModels.Report;

namespace YiHua.Pos.UI.Views.Report;

/// <summary>
/// 报表页。布局对齐参考端（左页签 + 时间范围 + 指标卡 + 未结账统计 + 分布）。
///
/// 口径都写在指标卡的副标题上，不靠猜：
///   净营业额 = 已结账实收 - 退款
///   折后笔单价 = 净营业额 / 订单笔数
/// 报表最容易出的问题是「两个页面的同一个词不是同一个意思」，
/// 所以每个数都标清楚是怎么算的。
/// </summary>
public partial class ReportHomeView : UserControl
{
    private bool _showingPaymentTab;

    public ReportHomeView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private ReportHomeViewModel? Vm => DataContext as ReportHomeViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ = LoadAsync(DateTime.Today, DateTime.Today.AddDays(1), "今日");
        UpdateTabHighlight();
    }

    // ---------- 数据加载 ----------

    public async Task LoadAsync(DateTime start, DateTime end, string label)
    {
        var vm = Vm;
        if (vm == null) return;

        await vm.LoadRangeAsync(start, end, label);
        Refresh();
    }

    private void Refresh()
    {
        var vm = Vm;
        if (vm == null) return;

        var rangeText = this.FindControl<TextBlock>("RangeText");
        if (rangeText != null)
        {
            rangeText.Text = vm.RangeLabel == "今日"
                ? $"{vm.ReportDate:yyyy/MM/dd}（{GetWeekday(vm.ReportDate)}）"
                : $"{vm.RangeLabel} · {vm.ReportDate:yyyy/MM/dd} 起";
        }

        SetText("NetTurnoverText", $"¥{vm.NetTurnover:F2}");
        SetText("RefundText", $"退款金额：¥{vm.RefundAmount:F2}");
        SetText("OrderCountText", $"{vm.OrderCount} 笔");
        SetText("RefundCountText", $"退款笔数：{vm.RefundCount} 笔");
        SetText("AvgTicketText", $"¥{vm.AvgTicketAfterDiscount:F2}");
        SetText("GrowthText", vm.RangeLabel == "今日"
            ? $"较昨日 {vm.TurnoverGrowth:+0.0%;-0.0%;0.0%}"
            : "折后笔单价 = 净营业额 / 订单笔数");
        SetText("DiscountText", $"¥{vm.DiscountTotal:F2}");
        SetText("ReceivedText", $"实收 ¥{vm.NetReceived:F2}");

        SetText("PendingCountText", $"{vm.PendingOrderCount} 笔");
        SetText("PendingAmountText", $"¥{vm.PendingAmount:F2}");
        SetText("PendingPaidText", $"¥{vm.PendingPaidAmount:F2}");
        SetText("PendingUnpaidText", $"¥{vm.PendingUnpaidAmount:F2}");

        var payList = this.FindControl<ItemsControl>("PayTypeList");
        if (payList != null) payList.ItemsSource = vm.PayTypeBreakdown;

        var orderTypeList = this.FindControl<ItemsControl>("OrderTypeList");
        if (orderTypeList != null) orderTypeList.ItemsSource = vm.OrderTypeBreakdown;

        var empty = this.FindControl<Border>("EmptyBox");
        if (empty != null) empty.IsVisible = vm.IsEmpty;

        var error = this.FindControl<TextBlock>("ErrorText");
        if (error != null) error.Text = vm.ErrorMessage ?? string.Empty;
    }

    private void SetText(string name, string text)
    {
        var tb = this.FindControl<TextBlock>(name);
        if (tb != null) tb.Text = text;
    }

    /// <summary>星期几（报表标题里要显示，参考端同款）。</summary>
    public static string GetWeekday(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日",
    };

    // ---------- 时间范围 ----------

    private async void OnRangeToday(object? s, RoutedEventArgs e)
        => await LoadAsync(DateTime.Today, DateTime.Today.AddDays(1), "今日");

    private async void OnRangeYesterday(object? s, RoutedEventArgs e)
    {
        var d = DateTime.Today.AddDays(-1);
        await LoadAsync(d, d.AddDays(1), "昨天");
    }

    private async void OnRangeThisWeek(object? s, RoutedEventArgs e)
    {
        // 周一为一周起点（国内习惯）
        var today = DateTime.Today;
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-offset);
        await LoadAsync(monday, today.AddDays(1), "本周");
    }

    private async void OnRangeThisMonth(object? s, RoutedEventArgs e)
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadAsync(first, DateTime.Today.AddDays(1), "本月");
    }

    private async void OnRangeLast7(object? s, RoutedEventArgs e)
        => await LoadAsync(DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1), "近 7 天");

    // ---------- 页签 ----------

    private async void OnTabBusiness(object? s, RoutedEventArgs e)
    {
        _showingPaymentTab = false;
        SetText("TitleText", "营业报表");
        UpdateTabHighlight();
        await LoadAsync(DateTime.Today, DateTime.Today.AddDays(1), "今日");
    }

    private async void OnTabPayment(object? s, RoutedEventArgs e)
    {
        _showingPaymentTab = true;
        SetText("TitleText", "收款报表");
        UpdateTabHighlight();
        await LoadAsync(DateTime.Today, DateTime.Today.AddDays(1), "今日");
    }

    private void UpdateTabHighlight()
    {
        var business = this.FindControl<Button>("TabBusiness");
        var payment = this.FindControl<Button>("TabPayment");
        if (business == null || payment == null) return;

        var on = new SolidColorBrush(Color.Parse("#2F6BFF"));
        var onText = new SolidColorBrush(Colors.White);
        var off = new SolidColorBrush(Colors.Transparent);
        var offText = new SolidColorBrush(Color.Parse("#1A2233"));

        business.Background = _showingPaymentTab ? off : on;
        business.Foreground = _showingPaymentTab ? offText : onText;
        payment.Background = _showingPaymentTab ? on : off;
        payment.Foreground = _showingPaymentTab ? onText : offText;
    }
}