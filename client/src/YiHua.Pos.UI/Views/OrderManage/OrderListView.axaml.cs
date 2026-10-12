using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class OrderListView : UserControl
{
    public OrderListView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private OrderListViewModel? ViewModel => DataContext as OrderListViewModel;

    public event EventHandler<OrderRowModel>? OrderSelected;

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ComboBox cb) return;
        var tag = (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        ViewModel.StatusFilter = tag switch
        {
            "Pending" => OrderManageStatus.Pending,
            "Settled" => OrderManageStatus.Settled,
            "Refunded" => OrderManageStatus.Refunded,
            "Reversed" => OrderManageStatus.Reversed,
            _ => null,
        };
    }

    private void OnToggleSort(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.SortByAmountDesc = !ViewModel.SortByAmountDesc;
    }

    private void OnClearFilter(object? s, RoutedEventArgs e) => ViewModel?.ClearFilter();

    private void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is OrderRowModel row)
            OrderSelected?.Invoke(this, row);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ViewModel != null)
            _ = ViewModel.LoadAsync();
    }
}
