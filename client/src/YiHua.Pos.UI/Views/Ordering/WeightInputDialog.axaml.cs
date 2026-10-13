using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>称重输入弹窗；只在有效重量确认时返回结果。</summary>
public partial class WeightInputDialog : Window
{
    public WeightInputDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private WeightInputViewModel? Vm => DataContext as WeightInputViewModel;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (Vm != null && Vm.TryConfirm(out _)) Close();
    }
}