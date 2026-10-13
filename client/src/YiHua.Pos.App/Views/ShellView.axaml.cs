using Avalonia.Controls;
using Prism.Ioc;
using YiHua.Pos.App.Services;
using YiHua.Pos.App.ViewModels;

namespace YiHua.Pos.App.Views;

public partial class ShellView : Window
{
    private readonly ShellViewModel _vm;

    public ShellView()
    {
        InitializeComponent();
        _vm = new ShellViewModel(new PrismServiceProvider(App.CurrentContainer), this);
        DataContext = _vm;
        Opened += async (_, _) => await _vm.InitializeAsync();
    }
}