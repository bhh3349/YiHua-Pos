using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftDifferenceDialog : UserControl
{
    public ShiftDifferenceDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ShiftDifferenceViewModel? ViewModel => DataContext as ShiftDifferenceViewModel;
    public event EventHandler? Cancelled;
    public event EventHandler? Confirmed;

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
    private void OnConfirm(object? s, RoutedEventArgs e) => Confirmed?.Invoke(this, EventArgs.Empty);
}
