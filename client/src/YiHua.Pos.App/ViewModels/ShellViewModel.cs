using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services;
using YiHua.Pos.UI.Views.Desk;
using YiHua.Pos.UI.Views.Ordering;
using YiHua.Pos.UI.Views.Shift;
using YiHua.Pos.UI.ViewModels.Desk;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.App.ViewModels;

/// <summary>导航项：对应左侧菜单的一项。</summary>
public sealed class NavItem : BindableBase
{
    public NavItem(string key, string title, string hint, Func<IServiceProvider, object>? factory = null)
    {
        Key = key;
        Title = title;
        Hint = hint;
        Factory = factory;
    }

    /// <summary>导航键。</summary>
    public string Key { get; }

    /// <summary>菜单文字。</summary>
    public string Title { get; }

    /// <summary>副标题，说明这个页面干什么。</summary>
    public string Hint { get; }

    /// <summary>创建该页面视图的工厂；为 null 表示该页尚未接线。</summary>
    public Func<IServiceProvider, object>? Factory { get; }

    /// <summary>是否已接线。未接线的菜单项置灰，避免「点了没反应」。</summary>
    public bool IsWired => Factory is not null;
}

/// <summary>
/// Shell 视图模型：管「当前显示哪个页面」。
///
/// 单页切换，不做标签页 —— 收银员一次只干一件事，多标签反而让他分不清在哪。
/// </summary>
public sealed class ShellViewModel : BindableBase
{
    private readonly IServiceProvider _services;
    private NavItem? _current;
    private object? _page;

    /// <summary>取容器里的服务；缺依赖时立刻报错，不要等到界面空白才发现。</summary>
    private static T Get<T>(IServiceProvider p) where T : class =>
        (T)(p.GetService(typeof(T))
            ?? throw new InvalidOperationException($"容器里没有注册 {typeof(T).Name}"));

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;

        NavItems = new ObservableCollection<NavItem>
        {
            // 开始干活
            new NavItem("desk", "桌台", "开台、清台、换台", p => new DeskMapView
            {
                DataContext = new DeskMapViewModel(Get<IDeskRepository>(p)),
            }),
            new NavItem("ordering", "点单", "堂食点餐", p => new OrderEntryView
            {
                DataContext = new OrderEntryViewModel(
                    Get<IProductRepository>(p),
                    Get<ICategoryRepository>(p),
                    Get<IOrderRepository>(p),
                    Get<IOutboxRepository>(p)),
            }),
            new NavItem("fastfood", "快餐", "外卖、叫号、预点"),

            // 收钱找零
            new NavItem("payment", "收银", "结算、支付方式"),
            new NavItem("order", "订单", "查单、补打、退菜"),

            // 处理异常
            new NavItem("hangorder", "挂单", "暂存、取回、拆单"),
            new NavItem("refund", "退款", "退菜、冲正"),
            new NavItem("shift", "交接班", "开班、交班、查班次", p => new ShiftCurrentView()),

            // 查账
            new NavItem("report", "报表", "营业额、商品排行、时段"),

            // 配置
            new NavItem("product", "商品", "菜品、分类、沽清"),
            new NavItem("settings", "设置", "打印机、门店、账号、诊断"),
            new NavItem("groupbuy", "团购券", "验券、核销记录"),
            new NavItem("member", "会员", "会员、充值、优惠券"),
        };

        Current = NavItems[0];
    }

    public ObservableCollection<NavItem> NavItems { get; }

    /// <summary>当前选中的导航项。</summary>
    public NavItem? Current
    {
        get => _current;
        set
        {
            if (SetProperty(ref _current, value))
                Page = value?.Factory?.Invoke(_services);
        }
    }

    /// <summary>内容区当前显示的控件。</summary>
    public object? Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    private DelegateCommand<object>? _navigateCommand;

    /// <summary>左侧菜单点击命令。参数是导航键。</summary>
    public DelegateCommand<object> NavigateCommand =>
        _navigateCommand ??= new DelegateCommand<object>(p =>
        {
            if (p is string key)
                Navigate(key);
        });

    /// <summary>按导航键切换页面。</summary>
    public void Navigate(string key)
    {
        var target = NavItems.FirstOrDefault(n => n.Key == key);
        if (target is not null)
            Current = target;
    }
}