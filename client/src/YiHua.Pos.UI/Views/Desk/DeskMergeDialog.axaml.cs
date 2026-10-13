using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskMergeDialog : UserControl
{
    public DeskMergeDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private DeskMergeViewModel? ViewModel => DataContext as DeskMergeViewModel;

    public event EventHandler? Cancelled;

    /// <summary>并台成功。</summary>
    public event EventHandler? Confirmed;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null && await ViewModel.ConfirmAsync())
            Confirmed?.Invoke(this, EventArgs.Empty);
    }
}