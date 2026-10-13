using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.ViewModels.Ordering;
// 别名必须在 namespace 内才生效：YiHua.Pos.UI.Views.Ordering 离 Product 子命名空间更近
using ProductEntity = YiHua.Pos.Domain.Entities.Product;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>
/// 点单页交互桥：把商品卡片点击、提交、结账转成业务事件。
/// 商品卡片用路由事件订阅，避免异步生成时漏挂。
/// </summary>
public partial class OrderEntryView : UserControl
{
    public OrderEntryView()
    {
        InitializeComponent();
        AddHandler(ProductCard.CardClickEvent, OnProductCardClick);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private OrderEntryViewModel? Vm => DataContext as OrderEntryViewModel;

    public event EventHandler<ProductEntity>? ProductClicked;
    public event EventHandler? SubmitRequested;
    public event EventHandler? CheckoutRequested;
    public event EventHandler<OrderLineViewModel>? LineSelected;
    public event EventHandler<string>? StatusMessage;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Vm != null)
            _ = Vm.LoadAsync();
    }

    private void OnProductCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProductCard card && card.DataContext is ProductEntity product)
            ProductClicked?.Invoke(this, product);
    }

    private void OnSubmit(object? sender, RoutedEventArgs e) => SubmitRequested?.Invoke(this, EventArgs.Empty);

    private void OnCheckout(object? sender, RoutedEventArgs e) => CheckoutRequested?.Invoke(this, EventArgs.Empty);

    private void OnHang(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || !Vm.HasLines)
        {
            StatusMessage?.Invoke(this, "明细为空，没有可挂起的订单。");
            return;
        }
        var id = Vm.HangCurrentOrder();
        StatusMessage?.Invoke(this, $"已挂起订单 {id[..Math.Min(8, id.Length)]}");
    }
}