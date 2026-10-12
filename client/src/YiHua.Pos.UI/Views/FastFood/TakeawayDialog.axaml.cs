using Avalonia.Controls;
using Avalonia.Interactivity;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>就餐方式 + 餐具数量对话框</summary>
public partial class TakeawayDialog : UserControl
{
    public TakeawayDialog()
    {
        InitializeComponent();
    }

    private TakeawayViewModel? Vm => DataContext as TakeawayViewModel;

    private void OnDineIn(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.DineIn;
    }

    private void OnTakeAway(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.TakeAway;
    }

    private void OnDelivery(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.Delivery;
    }

    private void OnIncTableware(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.TablewareCount++;
    }

    private void OnDecTableware(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.TablewareCount--;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
        => Confirmed?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Confirmed;
}
