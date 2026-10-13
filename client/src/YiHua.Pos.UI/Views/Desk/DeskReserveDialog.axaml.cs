using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskReserveDialog : UserControl
{
    public DeskReserveDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private DeskReserveViewModel? ViewModel => DataContext as DeskReserveViewModel;

    public event EventHandler? Cancelled;

    /// <summary>预订成功，带回预订信息。</summary>
    public event EventHandler<ReservationInfo>? Confirmed;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (await vm.ConfirmAsync())
            Confirmed?.Invoke(this, new ReservationInfo
            {
                DeskId = vm.Desk.Id,
                DeskName = vm.Desk.Name,
                CustomerName = vm.CustomerName.Trim(),
                Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
                ReserveTime = vm.ReserveTime,
                GuestCount = vm.GuestCount,
            });
    }
}