using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Cards;

/// <summary>
/// 桌台卡片：桌号（大字）+ 人数 + 金额 + 开台时长。
/// 状态用底色区分（不用彩色边框）：空闲浅灰 / 占用品牌蓝 / 预订琥珀。
/// 长按出操作菜单（换台/并台/清台）。
/// </summary>
public partial class DeskCard : UserControl
{
    public static readonly StyledProperty<string> DeskNameProperty =
        AvaloniaProperty.Register<DeskCard, string>(nameof(DeskName), string.Empty);

    public static readonly StyledProperty<int> GuestCountProperty =
        AvaloniaProperty.Register<DeskCard, int>(nameof(GuestCount));

    public static readonly StyledProperty<decimal> CurrentAmountProperty =
        AvaloniaProperty.Register<DeskCard, decimal>(nameof(CurrentAmount));

    public static readonly StyledProperty<string> ElapsedTextProperty =
        AvaloniaProperty.Register<DeskCard, string>(nameof(ElapsedText), string.Empty);

    public static readonly StyledProperty<DeskCardStatus> StatusProperty =
        AvaloniaProperty.Register<DeskCard, DeskCardStatus>(nameof(Status), DeskCardStatus.Idle);

    public string DeskName
    {
        get => GetValue(DeskNameProperty);
        set => SetValue(DeskNameProperty, value);
    }

    public int GuestCount
    {
        get => GetValue(GuestCountProperty);
        set => SetValue(GuestCountProperty, value);
    }

    public decimal CurrentAmount
    {
        get => GetValue(CurrentAmountProperty);
        set => SetValue(CurrentAmountProperty, value);
    }

    public string ElapsedText
    {
        get => GetValue(ElapsedTextProperty);
        set => SetValue(ElapsedTextProperty, value);
    }

    public DeskCardStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// 点击桌台（冒泡路由事件）。
    ///
    /// 为什么用路由事件而不是普通 CLR 事件：桌台卡片在 ItemsControl 里，
    /// 数据是异步加载的，控件实例什么时候生成、生成几次都不确定。
    /// 逐个去挂 CLR 事件会漏（OnLoaded 时列表还是空的，等数据回来卡片才出现），
    /// 结果就是「点桌台没反应」。路由事件由父级一次性订阅，卡片后建也能收到。
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CardClickEvent =
        RoutedEvent.Register<DeskCard, RoutedEventArgs>("CardClick", RoutingStrategies.Bubble);

    /// <summary>点击桌台时触发（CLR 事件，保留给直接持有卡片实例的调用方）。</summary>
    public event EventHandler? CardClicked;

    /// <summary>长按请求操作菜单时触发。</summary>
    public event EventHandler? MenuRequested;

    private Border? _cardBorder;
    private TextBlock? _nameBlock;
    private TextBlock? _infoBlock;
    private TextBlock? _amountBlock;
    private DateTime _pressStart;
    private bool _longPressFired;

    public DeskCard()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _cardBorder = this.FindControl<Border>("CardBorder");
        _nameBlock = this.FindControl<TextBlock>("DeskNameText");
        _infoBlock = this.FindControl<TextBlock>("DeskInfoText");
        _amountBlock = this.FindControl<TextBlock>("AmountText");
        UpdateStatusVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
            UpdateStatusVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressStart = DateTime.UtcNow;
        _longPressFired = false;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_longPressFired) return;
        var duration = DateTime.UtcNow - _pressStart;
        if (duration.TotalMilliseconds >= 500)
        {
            _longPressFired = true;
            MenuRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RaiseCardClick();
        }
    }

    /// <summary>触发一次「点击桌台」：CLR 事件 + 冒泡路由事件都发。</summary>
    public void RaiseCardClick()
    {
        CardClicked?.Invoke(this, EventArgs.Empty);
        RaiseEvent(new RoutedEventArgs(CardClickEvent, this));
    }

    /// <summary>供测试/外部触发菜单请求。</summary>
    public void RequestMenu() => MenuRequested?.Invoke(this, EventArgs.Empty);

    private void UpdateStatusVisual()
    {
        if (_cardBorder == null) return;
        var (bgKey, nameKey, infoKey) = GetStatusStyle(Status);
        _cardBorder.Bind(Border.BackgroundProperty,
            new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(bgKey));
        if (_nameBlock != null)
            _nameBlock.Bind(TextBlock.ForegroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(nameKey));
        if (_infoBlock != null)
            _infoBlock.Bind(TextBlock.ForegroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(infoKey));
        if (_amountBlock != null)
            _amountBlock.Bind(TextBlock.ForegroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(nameKey));
    }

    /// <summary>
    /// 状态 → 视觉样式映射（可测试的纯逻辑）。
    /// 返回：背景/主文字/次要文字的资源键。占用态用白字，空闲/预订用深色字。
    /// </summary>
    public static (string BgKey, string NameKey, string InfoKey) GetStatusStyle(DeskCardStatus status) => status switch
    {
        DeskCardStatus.Occupied => ("DeskOccupiedBrush", "TextOnPrimaryBrush", "TextOnPrimaryBrush"),
        DeskCardStatus.Reserved => ("DeskReservedBrush", "TextPrimaryBrush", "TextPrimaryBrush"),
        _ => ("DeskIdleBrush", "TextPrimaryBrush", "TextSecondaryBrush"),
    };
}

public enum DeskCardStatus
{
    Idle,
    Occupied,
    Reserved,
}
