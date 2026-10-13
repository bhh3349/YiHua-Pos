using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>整单打折弹窗。</summary>
public partial class OrderDiscountDialog : Window
{
    public OrderDiscountDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private OrderDiscountViewModel? Vm => DataContext as OrderDiscountViewModel;

    /// <summary>确认打折，带回折扣率（1 = 不打折）。</summary>
    public event EventHandler<decimal>? Confirmed;

    private void OnQuickRate(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is decimal rate && Vm != null)
            Vm.Rate = rate;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        Confirmed?.Invoke(this, vm.Rate);
        Close();
    }
}