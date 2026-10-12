using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Feedback;

/// <summary>
/// 模态弹窗容器：居中卡片（圆角 14px），点击遮罩可关闭（可选）。
/// 弹窗内容通过 DialogContent 设置（避免与 UserControl.Content 冲突）。
/// </summary>
public partial class PosModal : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PosModal, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<object?> DialogContentProperty =
        AvaloniaProperty.Register<PosModal, object?>(nameof(DialogContent));

    public static readonly StyledProperty<bool> CloseOnOverlayClickProperty =
        AvaloniaProperty.Register<PosModal, bool>(nameof(CloseOnOverlayClick), true);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    public bool CloseOnOverlayClick
    {
        get => GetValue(CloseOnOverlayClickProperty);
        set => SetValue(CloseOnOverlayClickProperty, value);
    }

    /// <summary>请求关闭时触发（由宿主移除弹窗）。</summary>
    public event EventHandler? CloseRequested;

    public PosModal()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>请求关闭（可测试）。</summary>
    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (CloseOnOverlayClick)
            RequestClose();
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 阻止冒泡到遮罩，避免点卡片时误关
        e.Handled = true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => RequestClose();
}
