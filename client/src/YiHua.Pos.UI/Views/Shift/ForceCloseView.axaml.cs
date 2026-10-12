using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ForceCloseView : UserControl
{
    public ForceCloseView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ForceCloseViewModel? ViewModel => DataContext as ForceCloseViewModel;
    public event EventHandler? Cancelled;

    public string ManagerName { get; set; } = string.Empty;
    public string ManagerRole { get; set; } = string.Empty;

    private void OnReasonChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ComboBox cb) return;
        var tag = (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        ViewModel.Reason = tag switch
        {
            "Miscounted" => DifferenceReason.Miscounted,
            "WrongChange" => DifferenceReason.WrongChange,
            "PrivateOrder" => DifferenceReason.PrivateOrder,
            "NoRecord" => DifferenceReason.NoRecord,
            "Stolen" => DifferenceReason.Stolen,
            _ => DifferenceReason.Other,
        };
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.ConfirmAsync(ManagerName, ManagerRole);
    }
}
