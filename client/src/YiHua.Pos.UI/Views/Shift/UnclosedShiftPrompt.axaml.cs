using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class UnclosedShiftPrompt : UserControl
{
    public UnclosedShiftPrompt() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private UnclosedShiftViewModel? ViewModel => DataContext as UnclosedShiftViewModel;

    /// <summary>用户选择去交班</summary>
    public event EventHandler? GoCloseRequested;

    /// <summary>用户选择联系店长（强制交班）</summary>
    public event EventHandler? ContactManagerRequested;

    private void OnGoClose(object? s, RoutedEventArgs e) => GoCloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnContactManager(object? s, RoutedEventArgs e) => ContactManagerRequested?.Invoke(this, EventArgs.Empty);
}
