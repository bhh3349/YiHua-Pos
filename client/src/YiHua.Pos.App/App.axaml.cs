using Avalonia;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;
using YiHua.Pos.App.Diagnostics;
using YiHua.Pos.App.Services;
using YiHua.Pos.App.Views;
using YiHua.Pos.App.ViewModels;
using YiHua.Pos.Data;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.Hardware.Devices;
using YiHua.Pos.UI.Services;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.Services.Report;
using YiHua.Pos.UI.Services.Settings;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.App;

public partial class App : PrismApplication
{
    public static IContainerProvider? CurrentContainer { get; private set; }

    public override void Initialize()
    {
        AppLog.Install();
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
        AppLog.AttachUiThreadHandler();
    }

    protected override AvaloniaObject CreateShell()
    {
        CurrentContainer = Container;
        Container.Resolve<DbInitializer>();
        SeedDeskIfEmpty();
        SeedUnitsIfEmpty();
        return Container.Resolve<ShellView>();
    }

    /// <summary>新店首启建 12 张示例桌，使开台主流程立即可测；只有桌台表为空时种子化。</summary>
    private void SeedDeskIfEmpty()
    {
        var repo = Container.Resolve<IDeskRepository>();
        if (repo.GetDesksAsync().GetAwaiter().GetResult().Count != 0) return;
        var area = new YiHua.Pos.Domain.Entities.DeskArea { Id = 1, Name = "大厅", Sort = 1 };
        var desks = Enumerable.Range(1, 12).Select(i => new YiHua.Pos.Domain.Entities.Desk
        {
            Id = i,
            AreaId = area.Id,
            AreaName = area.Name,
            Name = $"A{i:00}",
            Capacity = 4,
            Status = YiHua.Pos.Domain.Enums.DeskStatus.Idle,
            Sort = i,
        }).ToList();
        repo.ReplaceAllAsync(new[] { area }, desks).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 预置标准计量单位。
    /// 单位不是商家的业务数据（份/例/杯/斤是通用度量），预置好能省掉
    /// 「装完软件 → 想加菜 → 发现没单位 → 满世界找单位管理」这一趟。
    /// 分类不预置：那是商家自己的菜单结构，替他决定反而不对。
    /// </summary>
    private void SeedUnitsIfEmpty()
    {
        var repo = Container.Resolve<IUnitRepository>();
        if (repo.GetAllAsync().GetAwaiter().GetResult().Count != 0) return;

        var names = new[] { "份", "例", "碗", "盘", "杯", "瓶", "只", "条", "个", "串", "斤", "两", "kg" };
        var units = names.Select((n, i) => new YiHua.Pos.Domain.Entities.ProductUnit
        {
            Id = i + 1,
            Name = n,
            Sort = i + 1,
        }).ToList();
        repo.ReplaceAllAsync(units).GetAwaiter().GetResult();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<PosDbContext>(_ => PosDbContext.CreateDefault());
        containerRegistry.RegisterSingleton<DbInitializer>(sp =>
        {
            var ctx = sp.Resolve<PosDbContext>();
            var init = new DbInitializer(ctx.Db, ctx.DatabasePath);
            init.InitializeAsync().GetAwaiter().GetResult();
            return init;
        });

        var ctx = new Lazy<PosDbContext>(() => Container.Resolve<PosDbContext>());
        containerRegistry.RegisterSingleton<IProductRepository>(_ => new ProductRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<ICategoryRepository>(_ => new CategoryRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IUnitRepository>(_ => new UnitRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IDeskRepository>(_ => new DeskRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IOrderRepository>(_ => new OrderRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IShiftRepository>(_ => new ShiftRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IOutboxRepository>(_ => new OutboxRepository(ctx.Value.Db));
        containerRegistry.RegisterSingleton<IDictionaryRepository>(_ => new DictionaryRepository(ctx.Value.Db));

        containerRegistry.RegisterSingleton<IPermissionService, PermissionService>();
        containerRegistry.RegisterSingleton<IProductDataSource, ProductDataSource>();
        containerRegistry.RegisterSingleton<IProductEditService, ProductEditService>();
        containerRegistry.RegisterSingleton<DiagnosticInfoProvider, AppDiagnosticInfoProvider>();
        containerRegistry.RegisterSingleton<IBackupFileProvider>(_ => new BackupFileProvider(ctx.Value));
        containerRegistry.RegisterSingleton<IHangOrderRepository>(_ => new InMemoryHangOrderRepository());
        containerRegistry.RegisterSingleton<IOrderDataProvider, OrderDataProvider>();
        containerRegistry.RegisterSingleton<IReportDataSource, AppReportDataSource>();
        containerRegistry.RegisterSingleton<ITicketQueue>(_ => new InMemoryTicketQueue("A"));
        containerRegistry.RegisterSingleton<SecondaryScreenManager>(_ => new SecondaryScreenManager());
        containerRegistry.RegisterSingleton<ICounterDisplay>(_ => new SecondaryCounterDisplay(Container.Resolve<SecondaryScreenManager>()));
        containerRegistry.RegisterSingleton<ISettingsStore>(_ => new PersistentSettingsStore(ctx.Value));
        containerRegistry.RegisterSingleton<ILoginCache>(_ => new LocalLoginCache(Container.Resolve<IDictionaryRepository>()));
        containerRegistry.RegisterSingleton<ILoginApi, LocalAuthApi>();
        containerRegistry.RegisterSingleton<ILoginService, LoginService>();
        containerRegistry.RegisterSingleton<IShiftService, ShiftService>();
        containerRegistry.RegisterSingleton<IRefundRepository>(_ => new InMemoryRefundRepository());
        containerRegistry.RegisterSingleton<IRefundPolicy>(_ => new DefaultRefundPolicy());
        containerRegistry.RegisterSingleton<KitchenNotifyService>(_ => new KitchenNotifyService());
        containerRegistry.RegisterSingleton<SettingsPageHost>(_ => new SettingsPageHost(
            Container.Resolve<ISettingsStore>(), Container.Resolve<IBackupFileProvider>(), Container.Resolve<DiagnosticInfoProvider>()));

        containerRegistry.RegisterSingleton<ShellViewModel>();
        containerRegistry.RegisterSingleton<ShellView>();
    }
}