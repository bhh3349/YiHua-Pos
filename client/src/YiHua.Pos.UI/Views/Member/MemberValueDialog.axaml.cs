using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Views.Member;

/// <summary>
/// 会员数值操作弹窗：充值 / 余额调整 / 积分调整。
///
/// 三种操作长得像（都是「填个数 + 确认」），所以共用一个窗口，
/// 按模式显隐不同的字段：
///   充值     → 快捷金额 + 收款方式，金额是**增量**
///   余额调整 → 原因必填，金额是**目标值**（商家说「他卡里应该是 80」）
///   积分调整 → 原因必填，数值是**目标值**
/// 增量还是目标值必须让界面说清楚，否则商家填错方向。
/// </summary>
public partial class MemberValueDialog : Window
{
    private MemberValueMode _mode = MemberValueMode.Recharge;
    private decimal _currentBalance;
    private int _currentPoints;
    private string _payTypeName = "现金";

    public MemberValueDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>确认操作。</summary>
    public event EventHandler<MemberValueInput>? Confirmed;

    /// <summary>充值快捷金额。</summary>
    public IReadOnlyList<decimal> QuickAmounts { get; } = new[] { 50m, 100m, 200m, 500m, 1000m };

    /// <summary>按模式配置界面。</summary>
    public void Setup(MemberValueMode mode, string memberLabel, decimal balance, int points)
    {
        _mode = mode;
        _currentBalance = balance;
        _currentPoints = points;

        this.FindControl<TextBlock>("MemberText")!.Text = memberLabel;
        this.FindControl<TextBlock>("CurrentBalanceText")!.Text = $"¥{balance:F2}";
        this.FindControl<TextBlock>("CurrentPointsText")!.Text = $"{points}";

        var title = this.FindControl<TextBlock>("TitleText")!;
        var valueLabel = this.FindControl<TextBlock>("ValueLabel")!;
        var quick = this.FindControl<ItemsControl>("QuickPanel")!;
        var payPanel = this.FindControl<StackPanel>("PayTypePanel")!;
        var reasonPanel = this.FindControl<StackPanel>("ReasonPanel")!;
        var confirm = this.FindControl<Controls.Buttons.PosButton>("ConfirmButton")!;

        switch (mode)
        {
            case MemberValueMode.Recharge:
                title.Text = "会员充值";
                valueLabel.Text = "充值金额（元）—— 在余额上增加";
                quick.IsVisible = true;
                quick.ItemsSource = QuickAmounts;
                payPanel.IsVisible = true;
                reasonPanel.IsVisible = false;
                confirm.Content = "确认充值";
                break;

            case MemberValueMode.AdjustBalance:
                title.Text = "余额调整";
                valueLabel.Text = "调整后余额（元）—— 直接改成这个数";
                quick.IsVisible = false;
                payPanel.IsVisible = false;
                reasonPanel.IsVisible = true;
                confirm.Content = "确认调整";
                break;

            case MemberValueMode.AdjustPoints:
                title.Text = "积分调整";
                valueLabel.Text = "调整后积分 —— 直接改成这个数";
                quick.IsVisible = false;
                payPanel.IsVisible = false;
                reasonPanel.IsVisible = true;
                confirm.Content = "确认调整";
                break;
        }

        HighlightPayType();
        UpdatePreview();
    }

    private void HighlightPayType()
    {
        var cash = this.FindControl<Button>("PayCash")!;
        var qr = this.FindControl<Button>("PayQr")!;

        var on = new SolidColorBrush(Color.Parse("#2F6BFF"));
        var onText = new SolidColorBrush(Colors.White);
        var off = new SolidColorBrush(Color.Parse("#F5F7FA"));
        var offText = new SolidColorBrush(Color.Parse("#1A2233"));

        cash.Background = _payTypeName == "现金" ? on : off;
        cash.Foreground = _payTypeName == "现金" ? onText : offText;
        qr.Background = _payTypeName == "扫码" ? on : off;
        qr.Foreground = _payTypeName == "扫码" ? onText : offText;
    }

    /// <summary>输入变化时实时显示「操作后是多少」，填错方向一眼能看出来。</summary>
    private void UpdatePreview()
    {
        var text = this.FindControl<TextBox>("ValueBox")?.Text?.Trim() ?? string.Empty;
        var after = this.FindControl<TextBlock>("AfterText")!;

        if (!decimal.TryParse(text, out var value))
        {
            after.Text = "—";
            return;
        }

        after.Text = _mode switch
        {
            MemberValueMode.Recharge => $"¥{_currentBalance + value:F2}",
            MemberValueMode.AdjustBalance => $"¥{value:F2}",
            _ => $"{value:0}",
        };
    }

    private void OnValueChanged(object? sender, TextChangedEventArgs e) => UpdatePreview();

    private void OnQuickAmount(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is decimal amount)
        {
            this.FindControl<TextBox>("ValueBox")!.Text = amount.ToString("0.##");
            UpdatePreview();
        }
    }

    private void OnPayCash(object? sender, RoutedEventArgs e)
    {
        _payTypeName = "现金";
        HighlightPayType();
    }

    private void OnPayQr(object? sender, RoutedEventArgs e)
    {
        _payTypeName = "扫码";
        HighlightPayType();
    }

    public void SetError(string? message)
        => this.FindControl<TextBlock>("ErrorText")!.Text = message ?? string.Empty;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var text = this.FindControl<TextBox>("ValueBox")!.Text?.Trim() ?? string.Empty;
        if (!decimal.TryParse(text, out var value))
        {
            SetError("请填一个数字");
            return;
        }

        Confirmed?.Invoke(this, new MemberValueInput
        {
            Mode = _mode,
            Value = value,
            Reason = this.FindControl<TextBox>("ReasonBox")?.Text?.Trim() ?? string.Empty,
            PayTypeName = _payTypeName,
        });
    }
}

/// <summary>数值操作模式。</summary>
public enum MemberValueMode
{
    /// <summary>充值：在余额上增加。</summary>
    Recharge,

    /// <summary>余额调整：改成指定值。</summary>
    AdjustBalance,

    /// <summary>积分调整：改成指定值。</summary>
    AdjustPoints,
}

/// <summary>数值操作输入。</summary>
public class MemberValueInput
{
    public MemberValueMode Mode { get; set; }
    public decimal Value { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PayTypeName { get; set; } = "现金";
}