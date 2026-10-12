using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Globalization;
using YiHua.Pos.UI.ViewModels.Refund;

namespace YiHua.Pos.UI.Views.Refund;

public partial class RefundOrderView : UserControl
{
    public RefundOrderView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private RefundOrderViewModel? ViewModel => DataContext as RefundOrderViewModel;

    /// <summary>字符串非空 → true（用于显示拒绝原因）</summary>
    public static IValueConverter NotEmpty { get; } = new FuncValueConverter<string?, bool>(
        s => !string.IsNullOrEmpty(s));

    public event EventHandler? ItemRefundRequested;
    public event EventHandler? OrderRefundRequested;
    public event EventHandler? VoidRequested;

    private void OnItemRefund(object? s, RoutedEventArgs e) => ItemRefundRequested?.Invoke(this, EventArgs.Empty);
    private void OnOrderRefund(object? s, RoutedEventArgs e) => OrderRefundRequested?.Invoke(this, EventArgs.Empty);
    private void OnVoid(object? s, RoutedEventArgs e) => VoidRequested?.Invoke(this, EventArgs.Empty);
}
