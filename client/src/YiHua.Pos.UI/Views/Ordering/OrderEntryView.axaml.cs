using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.Controls.Lists;
using YiHua.Pos.UI.ViewModels.Ordering;
// 别名必须在 namespace 内才生效：本文件所在命名空间离 Product 子命名空间更近
using ProductEntity = YiHua.Pos.Domain.Entities.Product;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>
/// 点单页交互桥：商品卡片点击、明细行加减删、提交/结账/挂单/备注/退菜。
/// 全部走路由事件，避免列表异步生成时漏挂事件。
/// </summary>
public partial class OrderEntryView : UserControl
{
    public OrderEntryView()
    {
        InitializeComponent();
        AddHandler(ProductCard.CardClickEvent, OnProductCardClick);
        AddHandler(OrderItemRow.IncreaseQtyEvent, OnLineIncrease);
        AddHandler(OrderItemRow.DecreaseQtyEvent, OnLineDecrease);
        AddHandler(OrderItemRow.DeleteLineEvent, OnLineDelete);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private OrderEntryViewModel? Vm => DataContext as OrderEntryViewModel;

    public event EventHandler<ProductEntity>? ProductClicked;
    public event EventHandler? SubmitRequested;
    public event EventHandler? CheckoutRequested;
    public event EventHandler? HangRequested;
    public event EventHandler<OrderLineViewModel>? RemarkRequested;
    public event EventHandler<OrderLineViewModel>? RefundLineRequested;
    public event EventHandler<string>? StatusMessage;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Vm != null)
            _ = Vm.LoadAsync();
    }

    // ---------- 商品区 ----------

    private void OnProductCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProductCard card && card.DataContext is ProductEntity product)
            ProductClicked?.Invoke(this, product);
    }

    // ---------- 明细行 ----------

    private void OnLineIncrease(object? sender, RoutedEventArgs e)
    {
        if (e.Source is OrderItemRow row && row.DataContext is OrderLineViewModel line)
            Vm?.IncrementLine(line);
    }

    private void OnLineDecrease(object? sender, RoutedEventArgs e)
    {
        if (e.Source is OrderItemRow row && row.DataContext is OrderLineViewModel line)
            Vm?.DecrementLine(line);
    }

    private void OnLineDelete(object? sender, RoutedEventArgs e)
    {
        if (e.Source is OrderItemRow row && row.DataContext is OrderLineViewModel line)
            Vm?.RemoveLine(line);
    }

    // ---------- 明细操作按钮 ----------

    /// <summary>取要操作的行：优先用列表选中行，没选就提示。</summary>
    private OrderLineViewModel? RequireSelectedLine()
    {
        var line = Vm?.SelectedLine;
        if (line == null)
            StatusMessage?.Invoke(this, "请先在明细里点一行，再操作。");
        return line;
    }

    private void OnRemark(object? sender, RoutedEventArgs e)
    {
        if (RequireSelectedLine() is { } line)
            RemarkRequested?.Invoke(this, line);
    }

    private void OnRefundLine(object? sender, RoutedEventArgs e)
    {
        if (RequireSelectedLine() is { } line)
            RefundLineRequested?.Invoke(this, line);
    }

    private void OnHang(object? sender, RoutedEventArgs e) => HangRequested?.Invoke(this, EventArgs.Empty);

    // ---------- 底部 ----------

    private void OnSubmit(object? sender, RoutedEventArgs e) => SubmitRequested?.Invoke(this, EventArgs.Empty);

    private void OnCheckout(object? sender, RoutedEventArgs e) => CheckoutRequested?.Invoke(this, EventArgs.Empty);
}