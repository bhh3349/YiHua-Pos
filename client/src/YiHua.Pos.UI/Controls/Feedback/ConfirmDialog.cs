using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Controls.Feedback;

/// <summary>
/// 确认对话框：标题 + 说明 + 取消/确定。
/// 确定按钮用 Danger 变体（危险操作）或 Primary（普通确认），通过 IsDangerous 切换。
/// 通常放在 PosModal 里使用。
/// </summary>
public partial class ConfirmDialog : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ConfirmDialog, string>(nameof(Title), "请确认");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ConfirmDialog, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<ConfirmDialog, string>(nameof(ConfirmText), "确定");

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<ConfirmDialog, string>(nameof(CancelText), "取消");

    public static readonly StyledProperty<bool> IsDangerousProperty =
        AvaloniaProperty.Register<ConfirmDialog, bool>(nameof(IsDangerous));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    public string CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <summary>是否为危险操作（确定按钮用 Danger 变体）。</summary>
    public bool IsDangerous
    {
        get => GetValue(IsDangerousProperty);
        set => SetValue(IsDangerousProperty, value);
    }

    /// <summary>用户点了确定。</summary>
    public event EventHandler? Confirmed;

    /// <summary>用户点了取消。</summary>
    public event EventHandler? Cancelled;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateConfirmVariant();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDangerousProperty)
            UpdateConfirmVariant();
    }

    private void UpdateConfirmVariant()
    {
        var btn = this.FindControl<Buttons.PosButton>("ConfirmButton");
        if (btn != null)
            btn.Variant = IsDangerous ? Buttons.PosButtonVariant.Danger : Buttons.PosButtonVariant.Primary;
    }

    /// <summary>模拟确认（可测试）。</summary>
    public void Confirm() => Confirmed?.Invoke(this, EventArgs.Empty);

    /// <summary>模拟取消（可测试）。</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Confirm();
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();
}
