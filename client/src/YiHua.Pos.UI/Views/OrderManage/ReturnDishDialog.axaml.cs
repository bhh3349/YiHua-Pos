using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class ReturnDishDialog : UserControl
{
    public ReturnDishDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ReturnDishViewModel? ViewModel => DataContext as ReturnDishViewModel;
    public event EventHandler? Cancelled;

    private void OnReasonChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ComboBox cb) return;
        var tag = (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        ViewModel.Reason = tag switch
        {
            "DontWant" => RefundReason.DontWant,
            "WrongDish" => RefundReason.WrongDish,
            "CustomerLeft" => RefundReason.CustomerLeft,
            "QualityIssue" => RefundReason.QualityIssue,
            _ => RefundReason.Other,
        };
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.ConfirmAsync();
    }
}
