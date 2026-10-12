using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskChangeDialog : UserControl
{
    public DeskChangeDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private DeskChangeViewModel? ViewModel => DataContext as DeskChangeViewModel;
    public event EventHandler? Cancelled;
    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.ConfirmAsync(sourceHasUnsettledItems: false);
    }
}
