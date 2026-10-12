using Avalonia;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;
using YiHua.Pos.App.Views;

namespace YiHua.Pos.App;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
    }

    /// <summary>主窗口（Prism 称为 Shell）</summary>
    protected override AvaloniaObject CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    /// <summary>注册服务 —— 后续每个模块的注册都收敛到这里</summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 硬件层
        // containerRegistry.RegisterSingleton<IReceiptPrinter, ...>();

        // 数据层
        // containerRegistry.RegisterSingleton<IPosDbContext, PosDbContext>();

        // 业务服务
        // containerRegistry.RegisterSingleton<IOrderService, OrderService>();
    }
}
