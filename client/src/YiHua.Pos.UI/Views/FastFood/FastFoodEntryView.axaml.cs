using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;
using DProduct = YiHua.Pos.Domain.Entities.Product;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>快餐点单主界面</summary>
public partial class FastFoodEntryView : UserControl
{
    public FastFoodEntryView()
    {
        InitializeComponent();
    }

    private FastFoodEntryViewModel? Vm => DataContext as FastFoodEntryViewModel;

    private void OnIssueTicket(object? sender, RoutedEventArgs e)
    {
        Vm?.IssueTicket(1);
    }

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long catId && Vm != null)
            Vm.SelectedCategoryId = catId;
    }

    private void OnProductClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.Tag is DProduct p && Vm != null)
        {
            var action = Vm.AddProduct(p);
            // NeedSpec / NeedWeight 由宿主弹出对应对话框（复用 T-303）
            if (action == AddProductAction.NeedSpec)
                SpecDialogRequested?.Invoke(this, p);
            else if (action == AddProductAction.NeedWeight)
                WeightDialogRequested?.Invoke(this, p);
        }
    }

    private void OnDineIn(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.DineIn;
    }

    private void OnTakeAway(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.TakeAway;
        TakeawayDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDelivery(object? sender, RoutedEventArgs e)
    {
        if (Vm != null) Vm.OrderType = OrderType.Delivery;
        DeliveryDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnSubmit(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;
        var result = await vm.SubmitAsync();
        SubmitCompleted?.Invoke(this, result);
    }

    public event EventHandler<DProduct>? SpecDialogRequested;
    public event EventHandler<DProduct>? WeightDialogRequested;
    public event EventHandler? TakeawayDialogRequested;
    public event EventHandler? DeliveryDialogRequested;
    public event EventHandler<SubmitResult>? SubmitCompleted;
}
