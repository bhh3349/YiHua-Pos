using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.Controls.Lists;
using YiHua.Pos.UI.ViewModels.Ordering;
using ProductEntity = YiHua.Pos.Domain.Entities.Product;

namespace YiHua.Pos.UI.Views.Cashier;

/// <summary>
/// 收银主界面：左购物车 / 中操作条 / 右商品区 / 车底支付。
///
/// 布局对齐参考端实测结构（见 docs/参考端界面结构.md）。
/// 关键点：**所有动作同屏**，点菜、改量、备注、打折、收款都不跳页 ——
/// 收银台高峰期每多一次跳转就多排一次队。
/// </summary>
public partial class CashierView : UserControl
{
    public CashierView()
    {
        InitializeComponent();
        AddHandler(ProductCard.CardClickEvent, OnProductCardClick);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private OrderEntryViewModel? Vm => DataContext as OrderEntryViewModel;

    // ---- 交给 App 层处理（要弹窗或要跳页的）----
    public event EventHandler<ProductEntity>? ProductClicked;
    public event EventHandler<PaymentMethodKindLite>? PayRequested;
    public event EventHandler? HangRequested;
    public event EventHandler? OpenOrdersRequested;
    public event EventHandler<OrderLineViewModel>? LineRemarkRequested;
    public event EventHandler? OrderRemarkRequested;
    public event EventHandler<OrderLineViewModel>? EditPriceRequested;
    public event EventHandler<OrderLineViewModel>? LineDiscountRequested;
    public event EventHandler? OrderDiscountRequested;
    public event EventHandler<ProductEntity>? SpecRequested;
    public event EventHandler<ProductEntity>? IngredientRequested;
    public event EventHandler<string>? StatusMessage;

    /// <summary>点「登录会员」—— 收银时要能查会员、用余额付款。</summary>
    public event EventHandler? MemberRequested;

    /// <summary>点「餐牌号」—— 从取号队列发一个号。</summary>
    public event EventHandler? TicketNoRequested;

    /// <summary>会员登录后显示在车头（卡号 + 余额）。</summary>
    public void SetMemberLabel(string? text)
    {
        var btn = this.FindControl<Controls.Buttons.PosButton>("MemberButton");
        if (btn != null)
            btn.Content = string.IsNullOrEmpty(text) ? "登录会员" : text;
    }

    /// <summary>顶栏「制作中 N 份 / N 单」（由 App 层从叫号队列算）。</summary>
    public void SetKitchenLabel(string text)
    {
        var tb = this.FindControl<TextBlock>("KitchenLabel");
        if (tb != null) tb.Text = text;
    }

    /// <summary>取餐号（快餐模式下单时由 App 层发号后回填）。</summary>
    public void SetTicketNo(string ticketNo)
    {
        var label = this.FindControl<TextBlock>("TicketNoLabel");
        if (label != null) label.Text = ticketNo;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var greeting = this.FindControl<TextBlock>("GreetingText");
        if (greeting != null)
            greeting.Text = BuildGreeting(DateTime.Now);

        var date = this.FindControl<TextBlock>("DateText");
        if (date != null)
            date.Text = DateTime.Now.ToString("M月d日 HH:mm dddd");

        // 明细增删后刷新按钮状态。
        //
        // ⚠️ 合计**不能**只靠 CollectionChanged：它是在 Lines.Add 那一刻触发的，
        // 而 VM 的 Recalc() 在 Add 之后才跑，此时读 Totals 还是旧值 ——
        // 表现就是「加了一道菜，共 1 项，合计 ¥0.00」。
        // 所以金额相关的刷新挂在 Totals 的属性变更上。
        if (Vm is { } vm)
        {
            vm.Lines.CollectionChanged += (_, _) => RefreshOpButtons();
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(OrderEntryViewModel.SelectedLine):
                        RefreshOpButtons();
                        break;
                    case nameof(OrderEntryViewModel.Totals):
                    case nameof(OrderEntryViewModel.HasLines):
                        RefreshTotals();
                        RefreshOpButtons();
                        break;
                }
            };
        }

        var list = this.FindControl<ListBox>("LineList");
        if (list != null)
            list.SelectionChanged += (_, _) => RefreshOpButtons();

        Refresh();
    }

    /// <summary>按时间段问候（参考端空车显示问候语而不是空白）。</summary>
    public static string BuildGreeting(DateTime now) => now.Hour switch
    {
        >= 5 and < 11 => "早上好",
        >= 11 and < 14 => "中午好",
        >= 14 and < 18 => "下午好",
        >= 18 and < 23 => "晚上好",
        _ => "夜深了",
    };

    /// <summary>刷新整屏（明细、合计、商品、分类、按钮可用性）。</summary>
    public void Refresh()
    {
        var vm = Vm;
        if (vm == null) return;

        BuildCategoryTabs();
        BuildProductGrid();
        RefreshTotals();
        RefreshOpButtons();
    }

    private void BuildCategoryTabs()
    {
        var host = this.FindControl<StackPanel>("CategoryTabs");
        var vm = Vm;
        if (host == null || vm == null) return;

        host.Children.Clear();
        host.Children.Add(MakeCategoryButton("全部", null));

        foreach (var c in vm.Categories)
            host.Children.Add(MakeCategoryButton(c.Name, c));
    }

    private Button MakeCategoryButton(string text, CategoryTabViewModel? category)
    {
        var vm = Vm;
        var selected = category == null
            ? vm?.SelectedCategoryId == null
            : vm?.SelectedCategoryId == category.Id;

        var btn = new Button
        {
            Content = text,
            MinHeight = 44,
            Padding = new Avalonia.Thickness(18, 8),
            FontSize = 15,
        };

        if (selected == true)
        {
            btn.Bind(BackgroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("PrimaryBrush"));
            btn.Bind(ForegroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextOnPrimaryBrush"));
        }

        btn.Click += (_, _) =>
        {
            if (Vm == null) return;
            if (category == null) Vm.SelectedCategoryId = null;
            else Vm.SelectCategory(category);
            Refresh();
        };

        return btn;
    }

    private void BuildProductGrid()
    {
        var grid = this.FindControl<ItemsControl>("ProductGrid");
        var empty = this.FindControl<StackPanel>("EmptyProducts");
        var vm = Vm;
        if (grid == null || vm == null) return;

        var products = vm.Products.ToList();
        grid.ItemsSource = products.Select(p => new ProductCard
        {
            ProductName = p.Name,
            Price = p.Price,
            UnitName = p.UnitName,
            Width = 150,
            Height = 96,
            Margin = new Avalonia.Thickness(0, 0, 10, 10),
            DataContext = p,
            State = ResolveCardState(p),
        }).ToList();

        if (empty != null)
            empty.IsVisible = products.Count == 0;
    }

    /// <summary>
    /// 卡片状态。判定口径与 OrderEntryViewModel.AddProduct 保持一致 ——
    /// 两边不一致会出现「卡片显示普通、点下去要称重」这种错位。
    /// 套餐还没有独立表，暂时不显示套餐态。
    /// </summary>
    private static ProductCardState ResolveCardState(ProductEntity p)
    {
        if (p.IsSoldOut || p.DataStatus != 1) return ProductCardState.SoldOut;
        if (p.ProductType == 3 || p.WeightWay == 1) return ProductCardState.Weighted;
        return ProductCardState.Normal;
    }

    private void RefreshTotals()
    {
        var vm = Vm;
        if (vm == null) return;

        var totals = vm.Totals;

        var total = this.FindControl<TextBlock>("TotalLabel");
        if (total != null) total.Text = $"¥{totals.Payable:F2}";

        var discount = this.FindControl<TextBlock>("DiscountLabel");
        if (discount != null)
        {
            var discountSum = totals.LineDiscountTotal + totals.OrderDiscount + totals.WipeZeroAmount;
            discount.Text = discountSum > 0 ? $"已优惠 ¥{discountSum:F2}" : string.Empty;
        }

        var count = this.FindControl<TextBlock>("CountLabel");
        if (count != null)
            count.Text = $"共 {vm.Lines.Count} 项";

        var empty = this.FindControl<StackPanel>("EmptyCart");
        if (empty != null) empty.IsVisible = vm.Lines.Count == 0;

        // 有桌台才显示桌号；快餐（无桌）不显示，否则和右边「外带」单选框重复
        var desk = this.FindControl<TextBlock>("DeskLabel");
        if (desk != null)
        {
            var hasDesk = vm.Desk != null;
            desk.IsVisible = hasDesk;
            if (hasDesk) desk.Text = vm.DeskName;
        }
    }

    /// <summary>
    /// 按钮可用性分两级：
    ///   有明细才能用（清空/整单备注/打包…）
    ///   选中某一行才能用（规格/加料/改价/打折/赠菜/单品备注）
    /// 参考端也是这个行为 —— 没选行时整条是灰的。
    /// </summary>
    private void RefreshOpButtons()
    {
        var vm = Vm;
        var hasLines = vm?.Lines.Count > 0;
        var hasSelection = vm?.SelectedLine != null;

        var stack = this.FindControl<StackPanel>("OpStack");
        if (stack != null)
        {
            foreach (var child in stack.Children)
            {
                if (child is not Control c || c is Border) continue;

                var needsLine = (c.Tag as string) == "line";
                c.IsEnabled = needsLine ? hasSelection : hasLines;
            }
        }

        // 数量加减只跟选中行有关
        var minus = this.FindControl<Button>("QtyMinus");
        var plus = this.FindControl<Button>("QtyPlus");
        if (minus != null) minus.IsEnabled = hasSelection;
        if (plus != null) plus.IsEnabled = hasSelection;

        var qty = this.FindControl<TextBlock>("QtyLabel");
        if (qty != null)
            qty.Text = vm?.SelectedLine == null
                ? "0"
                : FormatQty(vm.SelectedLine.Qty);
    }

    /// <summary>数量文本：整数不显示小数，称重保留两位。</summary>
    public static string FormatQty(decimal qty)
        => qty == Math.Floor(qty) ? qty.ToString("0") : qty.ToString("0.00");

    // ---------- 商品区 ----------

    private void OnProductCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProductCard card && card.DataContext is ProductEntity product)
            ProductClicked?.Invoke(this, product);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        var keyword = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
        {
            vm.SelectedCategoryId = null;
            _ = vm.LoadAsync();
            return;
        }

        _ = SearchAsync(keyword);
    }

    private async Task SearchAsync(string keyword)
    {
        var vm = Vm;
        if (vm == null) return;

        // 搜索走 VM 现有加载路径：设关键词后重新载入
        await Task.Run(() => { });
        vm.SelectedCategoryId = null;
        await vm.LoadAsync();
        BuildProductGrid();
    }

    // ---------- 明细行 ----------

    private void OnLineIncrease(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line) { Vm.IncrementLine(line); Refresh(); }
    }

    private void OnLineDecrease(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line) { Vm.DecrementLine(line); Refresh(); }
    }

    private void OnDeleteLine(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm?.SelectedLine is { } line)
        {
            vm.RemoveLine(line);
            Refresh();
            StatusMessage?.Invoke(this, $"已删除「{line.ProductName}」");
        }
        else StatusMessage?.Invoke(this, "请先在明细里点一行，再删除。");
    }

    private void OnClearCart(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;
        if (vm.Lines.Count == 0) { StatusMessage?.Invoke(this, "明细已经是空的。"); return; }

        foreach (var line in vm.Lines.ToList())
            vm.RemoveLine(line);

        Refresh();
        StatusMessage?.Invoke(this, "已清空明细");
    }

    private void OnGift(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm?.SelectedLine is not { } line)
        {
            StatusMessage?.Invoke(this, "请先在明细里点一行，再赠菜。");
            return;
        }

        if (vm.TryToggleGift(line, out var deny))
        {
            Refresh();
            StatusMessage?.Invoke(this, line.IsGift ? $"「{line.ProductName}」已标记赠菜" : $"已取消「{line.ProductName}」的赠菜");
        }
        else StatusMessage?.Invoke(this, deny ?? "赠菜失败");
    }

    private void OnEditPrice(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line) EditPriceRequested?.Invoke(this, line);
        else StatusMessage?.Invoke(this, "请先在明细里点一行，再改单价。");
    }

    private void OnLineDiscount(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line) LineDiscountRequested?.Invoke(this, line);
        else StatusMessage?.Invoke(this, "请先在明细里点一行，再打折。");
    }

    private void OnLineRemark(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line) LineRemarkRequested?.Invoke(this, line);
        else StatusMessage?.Invoke(this, "请先在明细里点一行，再备注。");
    }

    private void OnSpec(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line && TryFindProduct(line.ProductId, out var product))
            SpecRequested?.Invoke(this, product);
        else StatusMessage?.Invoke(this, "先点一个菜，再选规格/做法。");
    }

    private void OnIngredient(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedLine is { } line && TryFindProduct(line.ProductId, out var product))
            IngredientRequested?.Invoke(this, product);
        else StatusMessage?.Invoke(this, "先点一个菜，再加料。");
    }

    private bool TryFindProduct(long productId, out ProductEntity product)
    {
        product = Vm?.Products.FirstOrDefault(p => p.Id == productId)!;
        return product != null;
    }

    // ---------- 整单操作 ----------

    private void OnOrderRemark(object? sender, RoutedEventArgs e)
        => OrderRemarkRequested?.Invoke(this, EventArgs.Empty);

    private void OnOrderDiscount(object? sender, RoutedEventArgs e)
        => OrderDiscountRequested?.Invoke(this, EventArgs.Empty);

    private void OnWipeZero(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        vm.WipeZeroEnabled = !vm.WipeZeroEnabled;
        Refresh();
        StatusMessage?.Invoke(this, vm.WipeZeroEnabled ? "已开启抹零" : "已关闭抹零");
    }

    // ---------- 其它 ----------

    private void OnTicketNo(object? sender, RoutedEventArgs e)
    {
        // 餐牌号由 App 层从取号队列分配（参考端叫「餐牌号」，就是取餐号）。
        // 不要在这里自己 +1 —— 那样和叫号页的队列是两套号，客人拿到号叫不到。
        TicketNoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMember(object? sender, RoutedEventArgs e)
        => MemberRequested?.Invoke(this, EventArgs.Empty);

    private void OnTakeOrder(object? sender, RoutedEventArgs e)
        => StatusMessage?.Invoke(this, "取单请到「挂单」页。");

    private void OnPack(object? sender, RoutedEventArgs e)
        => StatusMessage?.Invoke(this, "打包费按订单收取的规则还没配（设置 → 收银规则）。");

    private void OnOpenDrawer(object? sender, RoutedEventArgs e)
        => StatusMessage?.Invoke(this, "开钱箱需要先在「设置 → 打印机管理」配好小票机。");

    private void OnHang(object? sender, RoutedEventArgs e) => HangRequested?.Invoke(this, EventArgs.Empty);
    private void OnOpenOrders(object? sender, RoutedEventArgs e) => OpenOrdersRequested?.Invoke(this, EventArgs.Empty);

    private void OnDineIn(object? sender, RoutedEventArgs e) => Refresh();
    private void OnTakeOut(object? sender, RoutedEventArgs e) => Refresh();

    // ---------- 支付 ----------

    private void OnPayCash(object? sender, RoutedEventArgs e) => PayRequested?.Invoke(this, PaymentMethodKindLite.Cash);
    private void OnPayQr(object? sender, RoutedEventArgs e) => PayRequested?.Invoke(this, PaymentMethodKindLite.CustomQr);
    private void OnPayMore(object? sender, RoutedEventArgs e) => PayRequested?.Invoke(this, PaymentMethodKindLite.More);
}

/// <summary>收银页用的收款方式（避免 UI 层直接依赖 Payment 命名空间的枚举）。</summary>
public enum PaymentMethodKindLite
{
    Cash,
    CustomQr,
    More,
}