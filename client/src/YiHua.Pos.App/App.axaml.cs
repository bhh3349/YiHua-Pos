using Avalonia;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;
using YiHua.Pos.App.Views;
using YiHua.Pos.App.ViewModels;
using YiHua.Pos.Data;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services;

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
        // 之前这里 Resolve&lt;MainWindow&gt;()，而 MainWindow 显示的是
        // 「项目骨架已就绪」的占位页 —— 87 个界面一个都进不去。
        // 建表必须在 Shell 之前。DbInitializer 执行的是幂等 SQL，
        // 之前只在 RegisterTypes 里注册却没人 resolve，
        // V001_Init.sql 从没执行过，于是界面报 no such table: DeskArea。
        // 容器是懒加载：光注册不 resolve 等于没跑。
        Container.Resolve<DbInitializer>();

        // 这里原来 Resolve<MainWindow>()，而 MainWindow 显示的是
        // 「项目骨架已就绪」的占位页 —— 87 个界面一个都进不去。
        return Container.Resolve<ShellView>();
    }

    /// <summary>
    /// 服务注册 —— 所有模块的注册都收敛到这里。
    ///
    /// 之前这里全是注释掉的，导致 87 个界面写完了却一个都没接上：
    /// 启动显示的是「项目骨架已就绪」的占位页。
    ///
    /// 注册顺序：基础设施 → 数据 → 仓储 → 业务服务 → 页面。
    /// 容器是单例作用域，仓储共享同一个 SqlSugarClient（线程安全）。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // ---------- 基础设施 ----------
        containerRegistry.RegisterSingleton<PosDbContext>(_ => PosDbContext.CreateDefault());

        // 建表：幂等 SQL，跑一次长期有效。放在数据库注册之后、仓储之前。
        containerRegistry.RegisterSingleton<DbInitializer>(sp =>
        {
            var ctx = sp.Resolve<PosDbContext>();
            var init = new DbInitializer(ctx.Db, ctx.DatabasePath);
            init.InitializeAsync().GetAwaiter().GetResult();
            return init;
        });

        // ---------- 仓储（Domain 接口 → Data 实现）----------
        containerRegistry.RegisterSingleton<IProductRepository>(sp =>
            new ProductRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<ICategoryRepository>(sp =>
            new CategoryRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<IUnitRepository>(sp =>
            new UnitRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<IDeskRepository>(sp =>
            new DeskRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<IOrderRepository>(sp =>
            new OrderRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<IShiftRepository>(sp =>
            new ShiftRepository(sp.Resolve<PosDbContext>().Db));
        containerRegistry.RegisterSingleton<IOutboxRepository>(sp =>
            new OutboxRepository(sp.Resolve<PosDbContext>().Db));

        // ---------- 业务服务 ----------
        containerRegistry.RegisterSingleton<IPermissionService, PermissionService>();

        // ---------- Shell ----------
        containerRegistry.RegisterSingleton<ShellViewModel>();
        containerRegistry.RegisterSingleton<ShellView>();
    }
}