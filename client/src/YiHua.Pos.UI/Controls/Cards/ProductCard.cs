using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Cards;

/// <summary>
/// 商品卡片：三种尺寸（大图/中图/纯文字），四种状态（正常/售罄/称重/套餐）。
/// 按下反馈为底色变化（无缩放动画，高频操作）。
/// </summary>
public partial class ProductCard : UserControl
{
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<ProductCard, string>(nameof(ProductName), string.Empty);

    public static readonly StyledProperty<decimal> PriceProperty =
        AvaloniaProperty.Register<ProductCard, decimal>(nameof(Price));

    public static readonly StyledProperty<string> UnitNameProperty =
        AvaloniaProperty.Register<ProductCard, string>(nameof(UnitName), string.Empty);

    public static readonly StyledProperty<ProductCardSize> CardSizeProperty =
        AvaloniaProperty.Register<ProductCard, ProductCardSize>(nameof(CardSize), ProductCardSize.Medium);

    public static readonly StyledProperty<ProductCardState> StateProperty =
        AvaloniaProperty.Register<ProductCard, ProductCardState>(nameof(State), ProductCardState.Normal);

    public string ProductName
    {
        get => GetValue(ProductNameProperty);
        set => SetValue(ProductNameProperty, value);
    }

    public decimal Price
    {
        get => GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    public string UnitName
    {
        get => GetValue(UnitNameProperty);
        set => SetValue(UnitNameProperty, value);
    }

    public ProductCardSize CardSize
    {
        get => GetValue(CardSizeProperty);
        set => SetValue(CardSizeProperty, value);
    }

    public ProductCardState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// 点击商品（冒泡路由事件）。
    /// 同 DeskCard：商品卡片在 ItemsControl 里异步生成，CLR 事件容易漏挂，
    /// 用路由事件让父级一次性订阅。
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CardClickEvent =
        RoutedEvent.Register<ProductCard, RoutedEventArgs>("CardClick", RoutingStrategies.Bubble);

    /// <summary>点击商品时触发。</summary>
    public event EventHandler? CardClicked;

    public ProductCard()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
            UpdateState();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateState();
    }

    private void UpdateState()
    {
        // 售罄：整体降灰；称重/套餐：右上角小标签（在 XAML 中通过绑定控制显隐）
        Opacity = State == ProductCardState.SoldOut ? 0.55 : 1.0;
        IsEnabled = State != ProductCardState.SoldOut;
    }

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (State == ProductCardState.SoldOut)
            return;
        CardClicked?.Invoke(this, EventArgs.Empty);
        RaiseEvent(new RoutedEventArgs(CardClickEvent, this));
    }

    /// <summary>状态标签文本（可测试的纯逻辑）。</summary>
    public static string GetStateTag(ProductCardState state) => state switch
    {
        ProductCardState.SoldOut => "售罄",
        ProductCardState.Weighted => "称重",
        ProductCardState.Package => "套餐",
        _ => string.Empty,
    };

    /// <summary>是否显示状态标签（可测试）。</summary>
    public static bool HasStateTag(ProductCardState state) =>
        state is ProductCardState.SoldOut or ProductCardState.Weighted or ProductCardState.Package;
}

public enum ProductCardSize
{
    /// <summary>大图（快餐模式）。</summary>
    Large,
    /// <summary>中图（正餐模式）。</summary>
    Medium,
    /// <summary>纯文字（商品多时）。</summary>
    TextOnly,
}

public enum ProductCardState
{
    Normal,
    SoldOut,
    Weighted,
    Package,
}
