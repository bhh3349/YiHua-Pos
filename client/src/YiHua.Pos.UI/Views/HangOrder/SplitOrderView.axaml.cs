using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using YiHua.Pos.UI.ViewModels.HangOrder;

namespace YiHua.Pos.UI.Views.HangOrder;

/// <summary>预结账/拆单（多选明细，实时金额）</summary>
public partial class SplitOrderView : UserControl
{
    public SplitOrderView()
    {
        InitializeComponent();
    }

    private SplitOrderViewModel? Vm => DataContext as SplitOrderViewModel;

    private void OnItemToggle(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is long itemId && Vm?.Splitter != null)
        {
            Vm.Splitter.ToggleSelect(itemId);
            UpdateSelectionVisual(b, Vm.Splitter.SelectedIds.Contains(itemId));
        }
    }

    private static void UpdateSelectionVisual(Border border, bool selected)
    {
        // 选中高亮：用主题强调色边框（2px）
        border.BorderThickness = new Avalonia.Thickness(selected ? 2 : 1);
        // 边框颜色通过资源切换（选中=Primary，默认=Divider）
        // 实际项目中用样式，此处简化处理
    }

    private void OnSelectAll(object? sender, RoutedEventArgs e)
    {
        Vm?.Splitter?.SelectAll();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
        => Confirmed?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;
}
