using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftCloseView : UserControl
{
    public ShiftCloseView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ShiftCloseViewModel? ViewModel => DataContext as ShiftCloseViewModel;

    public event EventHandler? Cancelled;

    /// <summary>操作人（由调用方设置）</summary>
    public string OperatorName { get; set; } = string.Empty;

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
            await ViewModel.ConfirmAsync(OperatorName);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // shiftId 由调用方通过 DataContext 传入后加载
    }
}
