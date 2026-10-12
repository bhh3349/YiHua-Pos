using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Controls.Feedback;

/// <summary>
/// 空状态：必须是"邀请行动的入口"，不是"暂无数据"。
/// 标题 + 说明 + 行动按钮（必填）。
/// </summary>
public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(ActionText), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>行动按钮文本（为空则不显示按钮）。</summary>
    public string ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>用户点击行动按钮。</summary>
    public event EventHandler? ActionClicked;

    public EmptyState()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>模拟点击行动按钮（可测试）。</summary>
    public void InvokeAction() => ActionClicked?.Invoke(this, EventArgs.Empty);

    private void OnActionClick(object? sender, RoutedEventArgs e) => InvokeAction();
}
