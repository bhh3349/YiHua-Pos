using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskClearDialog : UserControl
{
    public DeskClearDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private DeskClearViewModel? ViewModel => DataContext as DeskClearViewModel;

    public event EventHandler? Cancelled;

    /// <summary>清台成功。</summary>
    public event EventHandler? Confirmed;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null && await ViewModel.ConfirmAsync(isSettled: true))
            Confirmed?.Invoke(this, EventArgs.Empty);
    }
}