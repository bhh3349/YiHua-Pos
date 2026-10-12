using Avalonia.Controls;
using Avalonia.Interactivity;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>正餐/快餐模式切换</summary>
public partial class ModeSwitchView : UserControl
{
    public ModeSwitchView()
    {
        InitializeComponent();
    }

    private void OnDineInMode(object? sender, RoutedEventArgs e)
        => ModeSelected?.Invoke(this, StoreMode.DineIn);

    private void OnFastFoodMode(object? sender, RoutedEventArgs e)
        => ModeSelected?.Invoke(this, StoreMode.FastFood);

    public event EventHandler<StoreMode>? ModeSelected;
}
