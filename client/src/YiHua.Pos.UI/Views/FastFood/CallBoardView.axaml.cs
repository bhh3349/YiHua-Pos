using Avalonia.Controls;
using Avalonia.Interactivity;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>叫号屏（大字号，高对比）</summary>
public partial class CallBoardView : UserControl
{
    public CallBoardView()
    {
        InitializeComponent();
    }

    private void OnToggleAmount(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CallBoardViewModel vm)
            vm.ShowAmount = !vm.ShowAmount;
    }
}
