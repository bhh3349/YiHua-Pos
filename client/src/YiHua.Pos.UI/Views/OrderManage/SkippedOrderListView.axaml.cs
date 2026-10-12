using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class SkippedOrderListView : UserControl
{
    public SkippedOrderListView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public event EventHandler<OrderRowModel>? RestoreRequested;

    private void OnOrderSelected(object? sender, SelectionChangedEventArgs e) { }

    private void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is OrderRowModel row)
            RestoreRequested?.Invoke(this, row);
    }
}
