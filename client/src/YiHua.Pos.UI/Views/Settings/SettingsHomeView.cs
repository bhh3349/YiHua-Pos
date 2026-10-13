using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Settings;

namespace YiHua.Pos.UI.Views.Settings;

/// <summary>设置首页可导航面板，加载由对应 VM 生命周期管理。</summary>
public partial class SettingsHomeView : UserControl
{
    public SettingsHomeView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SettingsNavigationViewModel vm && vm.Selected == null)
            vm.Selected = vm.Items.FirstOrDefault();
    }
}