using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Lists;

/// <summary>
/// 订单明细行：商品名 / 规格 / 数量加减 / 小计 / 删除。
/// 赠菜有明显标记（不计价），退菜有删除线。
///
/// 操作走**冒泡路由事件**：明细行是随订单变化反复生成的，
/// 父级订阅一次就能收到所有行的操作，不会漏挂（卡片那边踩过这个坑）。
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

    public static readonly StyledProperty<bool> IsWeightedProperty =
        AvaloniaProperty.Register<OrderItemRow, bool>(nameof(IsWeighted));

    /// <summary>界面显示的数量文本（称重商品显示重量）。</summary>
    public static readonly StyledProperty<string> DisplayQtyProperty =
        AvaloniaProperty.Register<OrderItemRow, string>(nameof(DisplayQty), "1");

    /// <summary>数量加一（冒泡）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> IncreaseQtyEvent =
        RoutedEvent.Register<OrderItemRow, RoutedEventArgs>("IncreaseQty", RoutingStrategies.Bubble);

    /// <summary>数量减一（冒泡）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DecreaseQtyEvent =
        RoutedEvent.Register<OrderItemRow, RoutedEventArgs>("DecreaseQty", RoutingStrategies.Bubble);

    /// <summary>删除整行（冒泡）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DeleteLineEvent =
        RoutedEvent.Register<OrderItemRow, RoutedEventArgs>("DeleteLine", RoutingStrategies.Bubble);

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

    /// <summary>称重商品：数量显示为重量，加减按钮不生效（要改重量得重新称）。</summary>
    public bool IsWeighted
    {
        get => GetValue(IsWeightedProperty);
        set => SetValue(IsWeightedProperty, value);
    }

    public string DisplayQty
    {
        get => GetValue(DisplayQtyProperty);
        set => SetValue(DisplayQtyProperty, value);
    }

    /// <summary>小计：赠菜/退菜不计价。</summary>
    public decimal Subtotal => (IsGift || IsRefunded) ? 0m : Quantity * UnitPrice;

    public OrderItemRow() => InitializeComponent();

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

    /// <summary>小计文本（可测试的纯逻辑）：赠菜显示「赠」。</summary>
    public static string FormatSubtotal(decimal subtotal, bool isGift)
        => isGift ? "赠" : $"¥{subtotal:F2}";

    private void OnIncreaseClick(object? sender, RoutedEventArgs e)
    {
        // 称重商品不能靠加一改量，必须重新称
        if (IsWeighted) return;
        RaiseEvent(new RoutedEventArgs(IncreaseQtyEvent, this));
    }

    private void OnDecreaseClick(object? sender, RoutedEventArgs e)
    {
        if (IsWeighted) return;
        RaiseEvent(new RoutedEventArgs(DecreaseQtyEvent, this));
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(DeleteLineEvent, this));
}