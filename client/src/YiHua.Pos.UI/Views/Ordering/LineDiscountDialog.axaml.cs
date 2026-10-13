using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>单品打折 / 减免弹窗。</summary>
public partial class LineDiscountDialog : Window
{
    public LineDiscountDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private LineDiscountViewModel? Vm => DataContext as LineDiscountViewModel;

    /// <summary>确认减免，带回减免金额。</summary>
    public event EventHandler<decimal>? Confirmed;

    private void OnQuickAmount(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is decimal amount && Vm != null)
            Vm.Discount = amount;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        if (vm.ErrorMessage != null)
        {
            this.FindControl<TextBlock>("ErrorText")!.Text = vm.ErrorMessage;
            return;
        }

        if (vm.Discount <= 0m)
        {
            this.FindControl<TextBlock>("ErrorText")!.Text = "请输入大于 0 的减免金额";
            return;
        }

        Confirmed?.Invoke(this, vm.Discount);
        Close();
    }
}