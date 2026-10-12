using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Refund;

namespace YiHua.Pos.UI.Views.Refund;

public partial class RefundConfirmView : UserControl
{
    public RefundConfirmView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private RefundConfirmViewModel? ViewModel => DataContext as RefundConfirmViewModel;

    public event EventHandler? Cancelled;
    public event EventHandler? Confirmed;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel?.Validate() == true)
            Confirmed?.Invoke(this, EventArgs.Empty);
    }
}
