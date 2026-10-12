using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Lists;

/// <summary>
/// 订单明细行：商品名 / 规格 / 数量 / 单价 / 小计。
/// 赠菜有明显标记（不计价），退菜有删除线。
/// 操作（改量/改价/赠菜/删除）通过事件暴露，由页面决定交互形式。
/// </summary>
public partial class OrderItemRow : UserControl
{
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<OrderItemRow, string>(nameof(ProductName), string.Empty);

    public static readonly StyledProperty<string> SpecTextProperty =
        AvaloniaProperty.Register<OrderItemRow, string>(nameof(SpecText), string.Empty);

    public static readonly StyledProperty<decimal> QuantityProperty =
        AvaloniaProperty.Register<OrderItemRow, decimal>(nameof(Quantity), 1m);

    public static readonly StyledProperty<decimal> UnitPriceProperty =
        AvaloniaProperty.Register<OrderItemRow, decimal>(nameof(UnitPrice));

    public static readonly StyledProperty<bool> IsGiftProperty =
        AvaloniaProperty.Register<OrderItemRow, bool>(nameof(IsGift));

    public static readonly StyledProperty<bool> IsRefundedProperty =
        AvaloniaProperty.Register<OrderItemRow, bool>(nameof(IsRefunded));

    public string ProductName
    {
        get => GetValue(ProductNameProperty);
        set => SetValue(ProductNameProperty, value);
    }

    public string SpecText
    {
        get => GetValue(SpecTextProperty);
        set => SetValue(SpecTextProperty, value);
    }

    public decimal Quantity
    {
        get => GetValue(QuantityProperty);
        set => SetValue(QuantityProperty, value);
    }

    public decimal UnitPrice
    {
        get => GetValue(UnitPriceProperty);
        set => SetValue(UnitPriceProperty, value);
    }

    /// <summary>赠菜（不计价）。</summary>
    public bool IsGift
    {
        get => GetValue(IsGiftProperty);
        set => SetValue(IsGiftProperty, value);
    }

    /// <summary>已退菜（删除线）。</summary>
    public bool IsRefunded
    {
        get => GetValue(IsRefundedProperty);
        set => SetValue(IsRefundedProperty, value);
    }

    /// <summary>小计：赠菜/退菜不计价。</summary>
    public decimal Subtotal => (IsGift || IsRefunded) ? 0m : Quantity * UnitPrice;

    public event EventHandler? ChangeQuantityRequested;
    public event EventHandler? ChangePriceRequested;
    public event EventHandler? ToggleGiftRequested;
    public event EventHandler? DeleteRequested;

    public OrderItemRow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QuantityProperty || change.Property == UnitPriceProperty ||
            change.Property == IsGiftProperty || change.Property == IsRefundedProperty)
        {
            UpdateSubtotal();
            UpdateStateVisual();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateSubtotal();
        UpdateStateVisual();
    }

    private void UpdateSubtotal()
    {
        var tb = this.FindControl<TextBlock>("SubtotalText");
        if (tb != null)
            tb.Text = FormatSubtotal(Subtotal, IsGift);
    }

    private void UpdateStateVisual()
    {
        var nameBlock = this.FindControl<TextBlock>("NameText");
        if (nameBlock != null)
            nameBlock.TextDecorations = IsRefunded ? TextDecorations.Strikethrough : null;
        var giftTag = this.FindControl<Control>("GiftTag");
        if (giftTag != null)
            giftTag.IsVisible = IsGift;
    }

    /// <summary>小计文本（可测试的纯逻辑）：赠菜显示"赠"。</summary>
    public static string FormatSubtotal(decimal subtotal, bool isGift)
        => isGift ? "赠" : $"¥{subtotal:F2}";

    private void OnQtyClick(object? sender, RoutedEventArgs e) => ChangeQuantityRequested?.Invoke(this, EventArgs.Empty);
    private void OnPriceClick(object? sender, RoutedEventArgs e) => ChangePriceRequested?.Invoke(this, EventArgs.Empty);
    private void OnGiftClick(object? sender, RoutedEventArgs e) => ToggleGiftRequested?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClick(object? sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);
}
