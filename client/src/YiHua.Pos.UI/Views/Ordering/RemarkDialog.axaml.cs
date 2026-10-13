using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>备注弹窗（最多 30 字）。</summary>
public partial class RemarkDialog : Window
{
    public RemarkDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>确认备注，带回文本（空串表示清空备注）。</summary>
    public event EventHandler<string>? Confirmed;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, (DataContext as RemarkViewModel)?.Remark ?? string.Empty);
        Close();
    }
}