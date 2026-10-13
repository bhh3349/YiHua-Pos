using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.UI.Views.Product;

/// <summary>
/// 商品列表页：分类筛选 + 搜索 + 状态筛选 + ProductCard 网格。
///
/// 分类标签和商品卡片都是运行时生成的（数据异步加载），
/// 所以点击统一走路由事件 / 一次性订阅，不在 OnLoaded 里遍历控件挂事件。
/// </summary>
public partial class ProductListView : UserControl
{
    private bool _wired;

    public ProductListView() => InitializeComponent();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(ProductCard.CardClickEvent, OnProductCardClick);
    }

    private ProductListViewModel? Vm => DataContext as ProductListViewModel;

    /// <summary>点开某个商品（编辑）。</summary>
    public event EventHandler<ProductDto>? ProductSelected;

    /// <summary>点「+ 新建商品」。</summary>
    public event EventHandler? NewProductRequested;

    /// <summary>点「分类管理」。</summary>
    public event EventHandler? CategoryManageRequested;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var vm = Vm;
        if (vm == null) return;

        await vm.LoadAsync();
        BuildCategoryTabs();
        RefreshGrid();
    }

    private void BuildCategoryTabs()
    {
        var host = this.FindControl<StackPanel>("CategoryTabs");
        var vm = Vm;
        if (host == null || vm == null) return;

        host.Children.Clear();

        // 「全部」永远排第一：找菜时最常用
        host.Children.Add(MakeCategoryButton("全部", null, vm.SelectedCategoryId == null));
        foreach (var c in vm.Categories)
            host.Children.Add(MakeCategoryButton(c.Name, c.Id, vm.SelectedCategoryId == c.Id));
    }

    private Button MakeCategoryButton(string text, long? categoryId, bool selected)
    {
        var btn = new Button
        {
            Content = text,
            MinHeight = 44,
            Padding = new Avalonia.Thickness(16, 8),
            Tag = categoryId,
        };
        if (selected)
        {
            btn.Bind(BackgroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("PrimaryBrush"));
            btn.Bind(ForegroundProperty,
                new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextOnPrimaryBrush"));
        }
        btn.Click += (_, _) =>
        {
            if (Vm == null) return;
            Vm.SelectedCategoryId = categoryId;
            BuildCategoryTabs();
            RefreshGrid();
        };
        return btn;
    }

    private void RefreshGrid()
    {
        var grid = this.FindControl<ItemsControl>("ProductGrid");
        var vm = Vm;
        if (grid == null || vm == null) return;

        var list = vm.GetFiltered();
        grid.ItemsSource = list.Select(p => new ProductCard
        {
            ProductName = p.Name,
            Price = p.Price,
            UnitName = p.UnitName,
            Width = 160,
            Margin = new Avalonia.Thickness(0, 0, 12, 12),
            DataContext = p,
            State = ResolveCardState(p),
        }).ToList();

        // 计数和空状态都依赖筛选结果，筛完必须通知一次
        vm.RefreshView();
    }

    private static ProductCardState ResolveCardState(ProductDto p)
    {
        if (p.IsSoldOut) return ProductCardState.SoldOut;
        if (!p.IsOnSale) return ProductCardState.SoldOut;
        if (p.IsBundle) return ProductCardState.Package;
        if (p.IsWeighted) return ProductCardState.Weighted;
        return ProductCardState.Normal;
    }

    private void OnProductCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProductCard card && card.DataContext is ProductDto dto)
            ProductSelected?.Invoke(this, dto);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vm == null) return;
        Vm.SearchKeyword = (sender as TextBox)?.Text ?? string.Empty;
        RefreshGrid();
    }

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm == null || sender is not ComboBox cb) return;
        Vm.StatusFilter = cb.SelectedIndex switch
        {
            1 => ProductStatusFilter.OnSale,
            2 => ProductStatusFilter.SoldOut,
            3 => ProductStatusFilter.OffShelf,
            _ => ProductStatusFilter.All,
        };
        RefreshGrid();
    }

    private void OnNewProduct(object? sender, RoutedEventArgs e) => NewProductRequested?.Invoke(this, EventArgs.Empty);

    private void OnCategoryManage(object? sender, RoutedEventArgs e) => CategoryManageRequested?.Invoke(this, EventArgs.Empty);
}