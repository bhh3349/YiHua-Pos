using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Services.Payment;
using YiHua.Pos.UI.ViewModels.Payment;

namespace YiHua.Pos.UI.Views.Payment;

/// <summary>
/// 结账页：整单优惠 + 支付方式 + 实收找零 + 确认结账。
///
/// 优惠与支付方式都做成大按钮而不是下拉框 —— 收银台是触控为主，
/// 下拉框要多点一次，高峰期就是排队。
/// </summary>
public partial class PaymentView : UserControl
{
    public PaymentView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private PaymentViewModel? Vm => DataContext as PaymentViewModel;

    /// <summary>确认结账，带回支付方式与实收金额（由 App 层落单）。</summary>
    public event EventHandler<SettlementRequest>? SettleRequested;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        RefreshQuickAmounts();
        UpdateMethodHighlight();
        RefreshCashPanel();
    }

    // ---------- 整单优惠 ----------

    private void ApplyDiscount(DiscountType type, decimal value)
    {
        var vm = Vm;
        if (vm == null) return;

        try
        {
            vm.ApplyDiscount(new DiscountRequest
            {
                Type = type,
                Value = value,
                IsManager = true,   // 权限校验在 App 层按登录角色做
                OperatorName = "店长",
            });
            SetError(null);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }

        RefreshCashPanel();
    }

    private void OnRoundToJiao(object? sender, RoutedEventArgs e) => ApplyDiscount(DiscountType.RoundToJiao, 0);
    private void OnRoundToYuan(object? sender, RoutedEventArgs e) => ApplyDiscount(DiscountType.RoundToYuan, 0);

    private void OnRateDiscount(object? sender, RoutedEventArgs e)
    {
        if (!TryReadDiscountValue(out var value)) return;
        // 收银员习惯说「打九折」，也可能直接填 9 —— 两种都认
        if (value > 1m) value /= 10m;
        if (value <= 0m || value >= 1m)
        {
            SetError("折扣请填 0.1~0.99（九折填 0.9 或 9）");
            return;
        }
        ApplyDiscount(DiscountType.Rate, value);
    }

    private void OnFixedDiscount(object? sender, RoutedEventArgs e)
    {
        if (!TryReadDiscountValue(out var value)) return;
        ApplyDiscount(DiscountType.Fixed, value);
    }

    private void OnOverridePrice(object? sender, RoutedEventArgs e)
    {
        if (!TryReadDiscountValue(out var value)) return;
        ApplyDiscount(DiscountType.Override, value);
    }

    private void OnFreeOrder(object? sender, RoutedEventArgs e) => ApplyDiscount(DiscountType.Free, 0);

    private void OnClearDiscount(object? sender, RoutedEventArgs e)
    {
        Vm?.ClearDiscount();
        SetError(null);
        RefreshCashPanel();
    }

    private bool TryReadDiscountValue(out decimal value)
    {
        value = 0m;
        var text = this.FindControl<TextBox>("DiscountValueBox")?.Text?.Trim();
        if (string.IsNullOrEmpty(text) || !decimal.TryParse(text, out value))
        {
            SetError("请先填优惠值，再点对应的优惠按钮");
            return false;
        }
        SetError(null);
        return true;
    }

    // ---------- 支付方式 ----------

    private void OnSelectCash(object? sender, RoutedEventArgs e) => Select(PaymentMethodKind.Cash);
    private void OnSelectCustomQr(object? sender, RoutedEventArgs e) => Select(PaymentMethodKind.CustomQr);
    private void OnSelectCredit(object? sender, RoutedEventArgs e) => Select(PaymentMethodKind.Credit);
    private void OnSelectMember(object? sender, RoutedEventArgs e) => Select(PaymentMethodKind.MemberCard);
    private void OnSelectGroupBuy(object? sender, RoutedEventArgs e) => Select(PaymentMethodKind.GroupBuy);

    private void Select(PaymentMethodKind method)
    {
        Vm?.SelectMethod(method);
        SetError(null);
        UpdateMethodHighlight();
        RefreshCashPanel();
    }

    /// <summary>选中的支付方式用 Primary 实心，其余描边 —— 一眼看出当前选的是哪个。</summary>
    private void UpdateMethodHighlight()
    {
        var vm = Vm;
        if (vm == null) return;

        var map = new (string Name, PaymentMethodKind Kind)[]
        {
            ("MethodCash", PaymentMethodKind.Cash),
            ("MethodCustomQr", PaymentMethodKind.CustomQr),
            ("MethodCredit", PaymentMethodKind.Credit),
            ("MethodMember", PaymentMethodKind.MemberCard),
            ("MethodGroupBuy", PaymentMethodKind.GroupBuy),
        };

        foreach (var (name, kind) in map)
        {
            var btn = this.FindControl<Controls.Buttons.PosButton>(name);
            if (btn == null) continue;
            btn.Variant = vm.SelectedMethod == kind
                ? Controls.Buttons.PosButtonVariant.Primary
                : Controls.Buttons.PosButtonVariant.Secondary;
        }
    }

    // ---------- 实收 / 找零 ----------

    private void RefreshQuickAmounts()
    {
        var list = this.FindControl<ItemsControl>("QuickAmountList");
        if (list == null || Vm == null) return;
        list.ItemsSource = Vm.QuickAmounts;
    }

    /// <summary>只有现金才显示实收输入；其他方式实收自动等于应收。</summary>
    private void RefreshCashPanel()
    {
        var vm = Vm;
        var panel = this.FindControl<StackPanel>("CashPanel");
        if (panel != null && vm != null)
            panel.IsVisible = vm.SelectedMethod == PaymentMethodKind.Cash;

        RefreshQuickAmounts();

        var box = this.FindControl<TextBox>("ReceivedBox");
        if (box != null && vm != null && vm.SelectedMethod == PaymentMethodKind.Cash && vm.ReceivedAmount > 0)
            box.Text = vm.ReceivedAmount.ToString("0.00");
    }

    private void OnReceivedChanged(object? sender, TextChangedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;
        var text = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
        vm.SetReceivedAmount(decimal.TryParse(text, out var v) ? v : 0m);
    }

    private void OnExactAmount(object? sender, RoutedEventArgs e)
    {
        Vm?.SetExactAmount();
        RefreshCashPanel();
    }

    private void OnQuickAmount(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is decimal amount)
        {
            Vm?.SetReceivedAmount(amount);
            RefreshCashPanel();
        }
    }

    // ---------- 结账 ----------

    private void OnConfirmSettle(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        var error = vm.Validate();
        if (error != null)
        {
            SetError(error);
            return;
        }

        SetError(null);
        SettleRequested?.Invoke(this, new SettlementRequest
        {
            Method = vm.SelectedMethod,
            PayableAmount = vm.PayableAmount,
            ReceivedAmount = vm.SelectedMethod == PaymentMethodKind.Cash ? vm.ReceivedAmount : vm.PayableAmount,
            DiscountAmount = vm.DiscountAmount,
            DiscountType = vm.AppliedDiscountType,
        });
    }

    private void SetError(string? message)
        => this.FindControl<TextBlock>("ErrorText")!.Text = message ?? string.Empty;
}

/// <summary>结账请求（视图交给 App 层落单）。</summary>
public class SettlementRequest
{
    public PaymentMethodKind Method { get; set; }
    public decimal PayableAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DiscountType? DiscountType { get; set; }
}