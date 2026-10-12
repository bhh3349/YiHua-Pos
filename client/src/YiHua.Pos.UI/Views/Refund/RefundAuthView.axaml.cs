using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Refund;

namespace YiHua.Pos.UI.Views.Refund;

public partial class RefundAuthView : UserControl
{
    public RefundAuthView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private RefundAuthViewModel? ViewModel => DataContext as RefundAuthViewModel;

    public event EventHandler? Cancelled;
    public event EventHandler? Authorized;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel?.Validate() == true)
            Authorized?.Invoke(this, EventArgs.Empty);
    }
}
