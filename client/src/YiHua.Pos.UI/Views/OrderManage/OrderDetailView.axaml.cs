using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class OrderDetailView : UserControl
{
    public OrderDetailView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private OrderDetailViewModel? ViewModel => DataContext as OrderDetailViewModel;

    public event EventHandler? RefundRequested;
    public event EventHandler? ReverseRequested;
    public event EventHandler? ReprintRequested;
    public event EventHandler? SkipRequested;

    private void OnRefund(object? s, RoutedEventArgs e) => RefundRequested?.Invoke(this, EventArgs.Empty);
    private void OnReverse(object? s, RoutedEventArgs e) => ReverseRequested?.Invoke(this, EventArgs.Empty);
    private void OnReprint(object? s, RoutedEventArgs e) => ReprintRequested?.Invoke(this, EventArgs.Empty);
    private void OnSkip(object? s, RoutedEventArgs e) => SkipRequested?.Invoke(this, EventArgs.Empty);
}
