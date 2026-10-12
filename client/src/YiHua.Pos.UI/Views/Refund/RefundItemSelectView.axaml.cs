using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Refund;

namespace YiHua.Pos.UI.Views.Refund;

public partial class RefundItemSelectView : UserControl
{
    public RefundItemSelectView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private RefundItemSelectViewModel? ViewModel => DataContext as RefundItemSelectViewModel;

    public event EventHandler? Cancelled;
    public event EventHandler? NextRequested;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnNext(object? s, RoutedEventArgs e)
    {
        if (ViewModel?.ValidateAll() == true)
            NextRequested?.Invoke(this, EventArgs.Empty);
    }
}
