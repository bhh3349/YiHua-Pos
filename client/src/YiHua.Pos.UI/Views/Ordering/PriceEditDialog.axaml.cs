using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>
/// 改价弹窗（仅店长）。
/// 必须显示原价 + 二次确认：改价是敏感操作，收银员按错一位数就是钱的问题。
/// </summary>
public partial class PriceEditDialog : Window
{
    public PriceEditDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>确认改价，带回新单价。</summary>
    public event EventHandler<decimal>? Confirmed;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as PriceEditViewModel;
        if (vm == null) return;

        if (!vm.RequestConfirm())
        {
            // 第一次点只是让「请再次确认改价」显示出来，不关窗
            return;
        }

        Confirmed?.Invoke(this, vm.NewPrice);
        Close();
    }
}