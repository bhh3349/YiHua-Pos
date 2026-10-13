using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using YiHua.Pos.App.Services;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.Services.Ordering;
using YiHua.Pos.UI.Services.Report;
using YiHua.Pos.UI.Services.Settings;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.ViewModels.Desk;
using YiHua.Pos.UI.ViewModels.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;
using YiHua.Pos.UI.ViewModels.OrderManage;
using YiHua.Pos.UI.ViewModels.Payment;
using YiHua.Pos.UI.ViewModels.Product;
using YiHua.Pos.UI.ViewModels.Report;
using YiHua.Pos.UI.ViewModels.Settings;
using YiHua.Pos.UI.Views.Desk;
using YiHua.Pos.UI.Views.FastFood;
using YiHua.Pos.UI.Views.HangOrder;
using YiHua.Pos.UI.Views.OrderManage;
using YiHua.Pos.UI.Views.Ordering;
using YiHua.Pos.UI.Views.Payment;
using YiHua.Pos.UI.Views.Product;
using YiHua.Pos.UI.Views.Refund;
using YiHua.Pos.UI.Views.Report;
using YiHua.Pos.UI.Views.Settings;
using YiHua.Pos.UI.Views.Shift;

namespace YiHua.Pos.App.ViewModels;

/// <summary>导航项：一项真实功能入口。</summary>
public sealed class NavItem : BindableBase
{
    public NavItem(string key, string title, string hint, Func<IServiceProvider, object> factory)
    {
        Key = key;
        Title = title;
        Hint = hint;
        Factory = factory;
    }
    public string Key { get; }
    public string Title { get; }
    public string Hint { get; }
    public Func<IServiceProvider, object> Factory { get; }
}

/// <summary>
/// 主程序编排：导航 + 业务状态。
/// 桌台/点单/收银共享同一个 Session，避免切页后丢当前订单。
/// </summary>
public sealed class ShellViewModel : BindableBase
{
    private readonly IServiceProvider _services;
    private readonly Window _shellWindow;
    private readonly IDeskRepository _desks;
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IOutboxRepository _outbox;
    private readonly IShiftRepository _shiftRepository;
    private readonly IShiftService _shiftService;
    private readonly IReportDataSource _reports;
    private readonly IProductDataSource _productData;
    private readonly IProductEditService _productEdit;
    private readonly ISettingsStore _settings;
    private readonly IBackupFileProvider _backup;
    private readonly DiagnosticInfoProvider _diagnostics;
    private readonly IPermissionService _permissions;
    private readonly IRefundRepository _refunds;
    private readonly IRefundPolicy _refundPolicy;
    private readonly KitchenNotifyService _kitchen;
    private readonly ILoginService _login;
    private readonly HangOrderStore _hangStore = new();

    private NavItem? _current;
    private object? _page;
    private string? _pageError;
    private Desk? _currentDesk;
    private Order? _currentOrder;
    private OrderEntryViewModel? _ordering;
    private string _operatorName = "店长";
    private bool _isLoggedIn;
    private bool _isOffline;
    private string? _statusMessage;

    private static T Get<T>(IServiceProvider p) where T : class =>
        (T)(p.GetService(typeof(T)) ?? throw new InvalidOperationException($"容器缺少服务 {typeof(T).Name}"));

    public ShellViewModel(IServiceProvider services, Window shellWindow)
    {
        _services = services;
        _shellWindow = shellWindow;
        _desks = Get<IDeskRepository>(services);
        _orders = Get<IOrderRepository>(services);
        _products = Get<IProductRepository>(services);
        _categories = Get<ICategoryRepository>(services);
        _outbox = Get<IOutboxRepository>(services);
        _shiftRepository = Get<IShiftRepository>(services);
        _shiftService = Get<IShiftService>(services);
        _reports = Get<IReportDataSource>(services);
        _productData = Get<IProductDataSource>(services);
        _productEdit = Get<IProductEditService>(services);
        _settings = Get<ISettingsStore>(services);
        _backup = Get<IBackupFileProvider>(services);
        _diagnostics = Get<DiagnosticInfoProvider>(services);
        _permissions = Get<IPermissionService>(services);
        _refunds = Get<IRefundRepository>(services);
        _refundPolicy = Get<IRefundPolicy>(services);
        _kitchen = Get<KitchenNotifyService>(services);
        _login = Get<ILoginService>(services);

        Login = new YiHua.Pos.UI.ViewModels.Login.LoginViewModel(_login, _permissions);
        Login.LoginSucceeded += (_, result) =>
        {
            _isLoggedIn = true;
            _isOffline = result.IsOffline;
            _operatorName = result.User?.DisplayName ?? result.User?.Username ?? "店长";
            RaisePropertyChanged(nameof(IsLoggedIn));
            RaisePropertyChanged(nameof(IsOffline));
            Navigate("desk");
        };

        NavItems = new ObservableCollection<NavItem>
        {
            new("desk", "桌台", "开台、清台、换台", p => CreateDeskView()),
            new("ordering", "点单", "堂食点餐", p => CreateOrderingView()),
            new("fastfood", "快餐", "外带、叫号、预点", p => CreateFastFoodView()),
            new("payment", "收银", "结算、支付方式", p => CreatePaymentView()),
            new("order", "订单", "查单、补打、退菜", p => CreateOrderListViewAsync().GetAwaiter().GetResult()),
            new("hangorder", "挂单", "暂存、取回、拆单", p => CreateHangOrderView()),
            new("refund", "退款", "退菜、冲正", p => CreateRefundLanding()),
            new("shift", "交接班", "开班、交班、查班次", p => CreateShiftView()),
            new("report", "报表", "营业额、商品排行、时段", p => CreateReportViewAsync().GetAwaiter().GetResult()),
            new("product", "商品", "菜品、分类、沽清", p => CreateProductViewAsync().GetAwaiter().GetResult()),
            new("settings", "设置", "门店、打印机、收银规则", p => CreateSettingsView()),
            new("about", "关于 / 诊断", "版本、诊断报告", p => CreateDiagnosticView()),
        };

        Current = NavItems[0];
        if (Login.StoreSelect.EffectiveStoreId != null)
        {
            _isLoggedIn = true;
            _isOffline = true;
            _permissions.SetRole("店长");
            RaisePropertyChanged(nameof(IsLoggedIn));
            RaisePropertyChanged(nameof(IsOffline));
        }
    }

    public ObservableCollection<NavItem> NavItems { get; }
    public YiHua.Pos.UI.ViewModels.Login.LoginViewModel Login { get; }
    public Desk? CurrentDesk => _currentDesk;
    public Order? CurrentOrder => _currentOrder;
    public bool IsLoggedIn { get => _isLoggedIn; private set => SetProperty(ref _isLoggedIn, value); }
    public bool IsOffline { get => _isOffline; private set => SetProperty(ref _isOffline, value); }
    public string? StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public NavItem? Current
    {
        get => _current;
        set => Activate(value);
    }

    /// <summary>
    /// 切到某个导航项并重建它的页面。
    ///
    /// **每次点击都要重建**，不能因为「还是同一个导航项」就跳过 ——
    /// 商品页下面还挂着「分类管理」「编辑商品」这些二级页，
    /// 从二级页想回列表，靠的就是再点一次「商品」。
    /// 之前用 SetProperty 的「值没变就返回」，结果点了没反应，
    /// 进去就出不来。
    /// </summary>
    private void Activate(NavItem? value)
    {
        var changed = !ReferenceEquals(_current, value);
        _current = value;
        if (changed)
            RaisePropertyChanged(nameof(Current));

        try
        {
            Page = value?.Factory(_services);
            Diagnostics.AppLog.Info($"切换到页面 {value?.Key}");
        }
        catch (Exception ex)
        {
            Diagnostics.AppLog.Error($"页面 {value?.Key} 打开失败: {ex}");
            Page = null;
            PageError = $"页面「{value?.Title}」打开失败：{ex.Message}";
        }
    }

    public object? Page { get => _page; private set { if (SetProperty(ref _page, value)) PageError = null; } }
    public string? PageError { get => _pageError; private set => SetProperty(ref _pageError, value); }

    private DelegateCommand<object>? _navigateCommand;
    public DelegateCommand<object> NavigateCommand => _navigateCommand ??= new DelegateCommand<object>(p =>
    {
        if (p is string key) Navigate(key);
    });

    private void Navigate(string key)
    {
        if (!_isLoggedIn && key != "login") key = "login";
        var target = NavItems.FirstOrDefault(n => n.Key == key);
        if (target != null) Current = target;
    }

    public async Task InitializeAsync()
    {
        await Login.InitializeAsync();
        if (Login.StoreSelect.SelectedStore == null && Login.StoreSelect.Stores.Count > 0)
            Login.StoreSelect.SelectedStore = Login.StoreSelect.Stores[0];
        if (!_isLoggedIn) Navigate("login");
    }

    private DeskMapView CreateDeskView()
    {
        var view = new DeskMapView { DataContext = new DeskMapViewModel(_desks) };
        view.DeskOpenRequested += async (_, desk) => await OpenDeskAsync(desk);
        view.OccupiedDeskSelected += async (_, desk) => await OpenOccupiedDeskAsync(desk);
        view.StatusMessage += (_, message) => StatusMessage = message;
        return view;
    }

    private async Task OpenFirstIdleDeskAsync()
    {
        var all = await _desks.GetDesksAsync();
        var desk = all.FirstOrDefault(x => x.Status == DeskStatus.Idle);
        if (desk == null)
        {
            StatusMessage = "当前没有空闲桌。请先到设置/桌台管理创建桌台。";
            return;
        }
        await OpenDeskAsync(desk);
    }

    private async Task OnDeskClickedAsync(DeskCardModel model)
    {
        var desk = await _desks.GetByIdAsync(model.Id);
        if (desk == null) { StatusMessage = "桌台已被删除，请刷新桌台列表。"; return; }
        if (desk.Status == DeskStatus.Idle) await OpenDeskAsync(desk);
        else if (desk.Status == DeskStatus.Occupied)
        {
            _currentDesk = desk;
            var pending = (await _orders.GetByDeskIdAsync(desk.Id)).FirstOrDefault(x => x.Status == OrderStatus.Pending);
            if (pending != null) _currentOrder = pending;
            _ordering = CreateOrderingViewModel(desk);
            if (_currentOrder != null) await LoadOrderLinesAsync(_currentOrder);
            Navigate("ordering");
        }
        else StatusMessage = $"{desk.Name} 已预订。";
    }

    private async Task OpenOccupiedDeskAsync(Desk desk)
    {
        _currentDesk = desk;
        _currentOrder = (await _orders.GetByDeskIdAsync(desk.Id)).FirstOrDefault(x => x.Status == OrderStatus.Pending);
        _ordering = CreateOrderingViewModel(desk);
        if (_currentOrder != null) await LoadOrderLinesAsync(_currentOrder);
        Navigate("ordering");
    }

    private async Task OpenDeskAsync(Desk desk)
    {
        var vm = new DeskOpenViewModel(_desks, desk);
        var dialog = new DeskOpenDialog { DataContext = vm };
        dialog.OpenedSuccessfully += async (_, _) =>
        {
            _currentDesk = await _desks.GetByIdAsync(desk.Id);
            _currentOrder = null;
            _ordering = CreateOrderingViewModel(_currentDesk!);
            Navigate("ordering");
        };
        await dialog.ShowDialog(_shellWindow);
    }

    private OrderEntryViewModel CreateOrderingViewModel(Desk? desk)
    {
        var vm = new OrderEntryViewModel(_products, _categories, _orders, _outbox, _hangStore,
            _permissions.CurrentRole == "店长" ? OperatorRole.Manager : OperatorRole.Cashier, desk);
        _ = vm.LoadAsync();
        return vm;
    }

    private async Task LoadOrderLinesAsync(Order order)
    {
        var fresh = await _orders.GetByIdAsync(order.Id);
        if (fresh == null || _ordering == null) return;
        _currentOrder = fresh;
        foreach (var item in fresh.Items)
            _ordering.AddLine(new OrderLineViewModel
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                SpecText = item.SpecText,
                Price = item.Price,
                Qty = item.Qty,
                LineDiscount = item.DiscountAmount,
                IsGift = item.IsGift,
                IsWeighted = item.IsWeighted,
            });
    }

    private OrderEntryView CreateOrderingView()
    {
        _ordering ??= CreateOrderingViewModel(_currentDesk);
        var view = new OrderEntryView { DataContext = _ordering };
        view.ProductClicked += (_, product) => HandleProductClick(product);
        view.SubmitRequested += async (_, _) => await SubmitOrderAsync();
        view.CheckoutRequested += async (_, _) => await NavigateToPaymentForCurrentOrderAsync();
        return view;
    }

    private void HandleProductClick(Product product)
    {
        if (_ordering == null) return;
        var action = _ordering.AddProduct(product);
        if (action == AddProductAction.SoldOut) StatusMessage = $"{product.Name} 已沽清";
        else if (action == AddProductAction.NeedWeight) ShowWeightDialog(product);
        else if (action == AddProductAction.NeedSpec) ShowSpecDialog(product);
    }

    private void ShowWeightDialog(Product product)
    {
        var vm = new WeightInputViewModel(product.Name, product.Price);
        var dialog = new WeightInputDialog { DataContext = vm };
        dialog.Closed += (_, _) =>
        {
            if (vm.TryConfirm(out _))
                _ordering?.AddLine(new OrderLineViewModel
                {
                    ProductId = product.Id, ProductName = product.Name, UnitName = product.UnitName,
                    Price = product.Price, IsWeighted = true, WeightKg = vm.WeightKg, Qty = vm.WeightKg,
                });
        };
        _ = dialog.ShowDialog((Window)Application.Current!.ApplicationLifetime!.GetType().GetProperty("MainWindow")!.GetValue(Application.Current.ApplicationLifetime)!);
    }

    private void ShowSpecDialog(Product product)
    {
        var options = product.SpecGroupName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => new YiHua.Pos.UI.ViewModels.Ordering.SpecOption { Group = "规格", Value = x }).ToList();
        var vm = new SpecSelectViewModel(product.Name, options);
        var dialog = new SpecSelectDialog { DataContext = vm };
        _ = dialog.ShowDialog((Window)Application.Current!.ApplicationLifetime!.GetType().GetProperty("MainWindow")!.GetValue(Application.Current.ApplicationLifetime)!);
        _ = vm.WaitResultAsync().ContinueWith(t =>
        {
            if (t.Result != null)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ordering?.AddLine(new OrderLineViewModel
                {
                    ProductId = product.Id, ProductName = product.Name, UnitName = product.UnitName,
                    Price = product.Price + t.Result.PriceDelta, Qty = 1, SpecText = t.Result.SpecText,
                }));
        });
    }

    private async Task SubmitOrderAsync()
    {
        if (_ordering == null) return;
        var result = await _ordering.SubmitAsync();
        if (!result.IsSuccess || result.OrderId == null)
        {
            StatusMessage = result.Error ?? (result.IsConflict ? "订单冲突，请刷新重试" : "下单失败");
            return;
        }
        _currentOrder = await _orders.GetByIdAsync(result.OrderId.Value);
        StatusMessage = $"已下单：{_currentOrder?.OrderNo ?? result.ClientOrderNo}";
    }

    private async Task<FastFoodEntryView> CreateFastFoodViewAsync()
    {
        var vm = new FastFoodEntryViewModel(_products, _categories, _orders, _outbox,
            new TicketQueueStateMachine(Get<ITicketQueue>(_services)));
        await vm.LoadAsync();
        return new FastFoodEntryView { DataContext = vm };
    }

    private FastFoodEntryView CreateFastFoodView() => CreateFastFoodViewAsync().GetAwaiter().GetResult();

    private async Task NavigateToPaymentForCurrentOrderAsync()
    {
        if (_currentOrder == null && _ordering != null)
            await SubmitOrderAsync();
        if (_currentOrder != null)
            Navigate("payment");
        else
            StatusMessage = "先下单，再结账。";
    }
    private PaymentView CreatePaymentView()
    {
        if (_currentOrder == null && _ordering != null)
        {
            StatusMessage = "先提交点菜单，再进入收银。";
            return new PaymentView { DataContext = new PaymentViewModel() };
        }
        var vm = new PaymentViewModel();
        if (_currentOrder != null)
        {
            vm.OrderNo = _currentOrder.OrderNo;
            vm.DeskInfo = _currentDesk?.Name ?? "快餐";
            foreach (var i in _currentOrder.Items)
                vm.Items.Add(new PaymentOrderItemDto { ProductName = i.ProductName, SpecText = i.SpecText, Quantity = i.Qty, Price = i.Price, Amount = i.Amount });
            vm.SubmitMachine.StateChanged += (_, state) =>
            {
                if (state == PaymentSubmitState.Succeeded && _currentOrder != null)
                    _ = SettleCurrentOrderAsync(vm.SelectedMethod,
                        vm.SelectedMethod == PaymentMethodKind.Cash ? vm.Items.Sum(i => i.Amount) : vm.Items.Sum(i => i.Amount),
                        vm.SelectedMethod.ToString());
            };
        }
        return new PaymentView { DataContext = vm };
    }

    private async Task SettleCurrentOrderAsync(PaymentMethodKind method, decimal received, string methodName)
    {
        if (_currentOrder == null) return;
        var order = await _orders.GetByIdAsync(_currentOrder.Id);
        if (order == null) { StatusMessage = "订单已不存在，刷新后重试。"; return; }
        if (order.Status != OrderStatus.Pending) { StatusMessage = "订单已结算或状态已变更。"; return; }

        var shift = await _shiftRepository.GetCurrentAsync();
        if (shift == null)
        {
            shift = await _shiftService.OpenAsync(0m, 1, _operatorName, Environment.MachineName);
        }

        var payType = method switch
        {
            PaymentMethodKind.Cash => PayType.Cash,
            PaymentMethodKind.CustomQr => PayType.Custom,
            PaymentMethodKind.Credit => PayType.Custom,
            _ => PayType.Custom,
        };
        if (method == PaymentMethodKind.Cash && received < order.AmountPay)
        {
            StatusMessage = "实收金额不足，结账未提交。";
            return;
        }
        if (method != PaymentMethodKind.Cash && received != order.AmountPay)
        {
            StatusMessage = "该收款方式要求实收等于应收。";
            return;
        }

        order.Payments.Add(new Payment { PayType = payType, PayTypeName = methodName, Amount = order.AmountPay, OperatorId = 1, PayTime = DateTime.Now });
        order.AmountReceived = received;
        order.AmountChange = Math.Max(0m, received - order.AmountPay);
        order.Status = OrderStatus.Settled;
        order.SettleTime = DateTime.Now;
        order.ShiftId = shift.Id;
        await _orders.UpdateAsync(order);

        shift.OrderCount++;
        shift.AmountTotal += order.AmountPay;
        if (payType == PayType.Cash) shift.CashAmount += order.AmountPay;
        else shift.CustomAmount += order.AmountPay;
        shift.CashShould += order.AmountPay;
        await _shiftRepository.UpdateAsync(shift);

        _currentOrder = order;
        StatusMessage = $"结账完成：应收 ¥{order.AmountPay:F2}，找零 ¥{order.AmountChange:F2}";
        _currentDesk = await _desks.GetByIdAsync(order.DeskId);
        if (_currentDesk != null)
        {
            _currentDesk.CurrentAmount = order.AmountPay;
            _currentDesk.CurrentOrderId = order.Id;
            await _desks.UpdateAsync(_currentDesk);
        }
    }

    private async Task<OrderListView> CreateOrderListViewAsync()
    {
        var vm = new OrderListViewModel(_orders);
        var view = new OrderListView { DataContext = vm };
        await vm.LoadAsync();
        view.OrderSelected += async (_, row) =>
        {
            var detail = new OrderDetailView { DataContext = new OrderDetailViewModel(_orders) };
            await ((OrderDetailViewModel)detail.DataContext!).LoadAsync(row.Id);
            Page = detail;
        };
        return view;
    }

    private HangOrderView CreateHangOrderView()
    {
        var store = _hangStore;
        return new HangOrderView { DataContext = new YiHua.Pos.UI.ViewModels.HangOrder.HangOrderViewModel(Get<IHangOrderRepository>(_services)) };
    }

    private UserControl CreateRefundLanding()
    {
        var root = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        root.Children.Add(new TextBlock { Text = "退款与冲正", FontSize = 24 });
        root.Children.Add(new TextBlock { Text = "从订单页选择订单后，点击退菜或反结账。" });
        return new UserControl { Content = root };
    }

    private ShiftCurrentView CreateShiftView() => new()
    {
        DataContext = new YiHua.Pos.UI.ViewModels.Shift.ShiftCurrentViewModel(_shiftService, _orders),
    };

    private async Task<ReportHomeView> CreateReportViewAsync()
    {
        var view = new ReportHomeView { DataContext = new ReportHomeViewModel(_reports) };
        await ((ReportHomeViewModel)view.DataContext).LoadAsync();
        return view;
    }

    private async Task<ProductListView> CreateProductViewAsync()
    {
        var view = new ProductListView { DataContext = new ProductListViewModel(_productData) };

        view.NewProductRequested += async (_, _) => await OpenProductEditAsync(null);
        view.CategoryManageRequested += async (_, _) => await OpenCategoryManageAsync();
        view.ProductSelected += async (_, dto) => await OpenProductEditAsync(dto);

        await view.LoadAsync();
        return view;
    }

    /// <summary>打开商品编辑页（product 为 null 表示新建）。</summary>
    private async Task OpenProductEditAsync(ProductDto? product)
    {
        var vm = new ProductEditViewModel(_productEdit, _productData);
        await vm.LoadAsync(product);

        var view = new ProductEditView { DataContext = vm };
        view.Saved += async (_, _) =>
        {
            StatusMessage = product == null ? $"已新增商品「{vm.Model.Name}」" : $"已保存「{vm.Model.Name}」";
            await ShowProductListAsync();
        };
        view.Cancelled += async (_, _) => await ShowProductListAsync();

        Page = view;
    }

    private async Task OpenCategoryManageAsync()
    {
        var vm = new CategoryManageViewModel(_productData, _productEdit);
        var view = new CategoryManageView { DataContext = vm };
        view.CloseRequested += async (_, _) => await ShowProductListAsync();

        Page = view;
    }

    /// <summary>回到商品列表并刷新（新增/编辑/改分类之后都要刷）。</summary>
    private async Task ShowProductListAsync()
    {
        var list = await CreateProductViewAsync();
        Page = list;
        NavigateToNavItem("product");
    }

    /// <summary>只改左侧高亮，不重建页面（避免把刚建好的页面又冲掉）。</summary>
    private void NavigateToNavItem(string key)
    {
        var target = NavItems.FirstOrDefault(n => n.Key == key);
        if (target != null && !ReferenceEquals(_current, target))
        {
            _current = target;
            RaisePropertyChanged(nameof(Current));
        }
    }

    private SettingsHomeView CreateSettingsView()
    {
        var view = new SettingsHomeView
        {
            DataContext = new SettingsPageHost(_settings, _backup, _diagnostics),
        };
        return view;
    }

    private YiHua.Pos.UI.Views.Settings.DiagnosticReportView CreateDiagnosticView()
    {
        var view = new YiHua.Pos.UI.Views.Settings.DiagnosticReportView
        {
            DataContext = new DiagnosticReportViewModel(_diagnostics),
        };
        _ = ((DiagnosticReportViewModel)view.DataContext).GenerateAsync();
        return view;
    }
}