using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.Services.Ordering;
using YiHua.Pos.UI.ViewModels.Ordering;

// 命名空间 YiHua.Pos.UI.ViewModels.Product 已存在（T-302），此处用别名避免 CS0118
using DProduct = YiHua.Pos.Domain.Entities.Product;
using DCategory = YiHua.Pos.Domain.Entities.ProductCategory;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>
/// 快餐点单 ViewModel。
/// 复用 T-303 的 OrderCalculator / OrderLineViewModel / SubmitStateMachine / 对话框 ViewModel。
/// 差异：无桌台（用取号）、先付后吃（未结账不能完成取号）。
/// </summary>
public sealed class FastFoodEntryViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly TicketQueueStateMachine _tickets;

    private Ticket? _ticket;
    private OrderTotals _totals = new();
    private OrderType _orderType = OrderType.DineIn;
    private bool _isSettled;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _blockWarning;

    public FastFoodEntryViewModel(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        IOrderRepository orderRepo,
        IOutboxRepository outboxRepo,
        TicketQueueStateMachine tickets,
        OperatorRole role = OperatorRole.Cashier)
    {
        _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
        _categoryRepo = categoryRepo ?? throw new ArgumentNullException(nameof(categoryRepo));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        Role = role;
        Submit = new SubmitStateMachine();
        Takeaway = new TakeawayViewModel();
        Delivery = new DeliveryViewModel();
    }

    public OperatorRole Role { get; }
    public SubmitStateMachine Submit { get; }
    public TakeawayViewModel Takeaway { get; }
    public DeliveryViewModel Delivery { get; }
    public TicketQueueStateMachine Tickets => _tickets;

    /// <summary>当前取号（如 "A052"）</summary>
    public string TicketNumber => _ticket?.Number ?? string.Empty;

    /// <summary>明细标题：取号 A052 · 3 位</summary>
    public string OrderTitleText =>
        _ticket == null ? "未取号" : $"取号 {_ticket.Number} · {_ticket.GuestCount} 位";

    /// <summary>就餐方式</summary>
    public OrderType OrderType
    {
        get => _orderType;
        set
        {
            if (SetProperty(ref _orderType, value))
                Takeaway.OrderType = value;
        }
    }

    /// <summary>是否已结账（先付后吃）</summary>
    public bool IsSettled
    {
        get => _isSettled;
        private set
        {
            if (SetProperty(ref _isSettled, value))
                RaisePropertyChanged(nameof(CanComplete));
        }
    }

    /// <summary>是否可以完成取号（必须先结账）</summary>
    public bool CanComplete => IsSettled;

    /// <summary>阻断提示（未结账时尝试放走）</summary>
    public string? BlockWarning
    {
        get => _blockWarning;
        private set => SetProperty(ref _blockWarning, value);
    }

    public ObservableCollection<DCategory> Categories { get; } = new();
    public ObservableCollection<DProduct> Products { get; } = new();
    public ObservableCollection<OrderLineViewModel> Lines { get; } = new();

    public OrderTotals Totals
    {
        get => _totals;
        private set => SetProperty(ref _totals, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasLines => Lines.Count > 0;
    public bool HasTicket => _ticket != null;

    // ---------- 取号 ----------

    /// <summary>发号并绑定到当前点单</summary>
    public void IssueTicket(int guestCount = 1)
    {
        _ticket = _tickets.Issue(guestCount);
        IsSettled = false;
        BlockWarning = null;
        RaisePropertyChanged(nameof(TicketNumber));
        RaisePropertyChanged(nameof(OrderTitleText));
        RaisePropertyChanged(nameof(HasTicket));
    }

    /// <summary>绑定已有取号（叫号后）</summary>
    public void BindTicket(Ticket ticket)
    {
        _ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
        IsSettled = false;
        BlockWarning = null;
        RaisePropertyChanged(nameof(TicketNumber));
        RaisePropertyChanged(nameof(OrderTitleText));
        RaisePropertyChanged(nameof(HasTicket));
    }

    // ---------- 商品 ----------

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var cats = await _categoryRepo.GetAllAsync(ct).ConfigureAwait(false);
            var prods = await _productRepo.GetAllAsync(ct).ConfigureAwait(false);
            Categories.Clear();
            foreach (var c in cats.OrderBy(c => c.Sort)) Categories.Add(c);
            _allProducts = prods.OrderBy(p => p.Sort).ToList();
            FilterProducts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载商品失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<DProduct> _allProducts = new();
    private long? _selectedCategoryId;

    public long? SelectedCategoryId
    {
        get => _selectedCategoryId;
        set { if (SetProperty(ref _selectedCategoryId, value)) FilterProducts(); }
    }

    private void FilterProducts()
    {
        Products.Clear();
        var list = _selectedCategoryId == null
            ? _allProducts
            : _allProducts.Where(p => p.CategoryId == _selectedCategoryId.Value);
        foreach (var p in list) Products.Add(p);
    }

    /// <summary>点商品（复用 T-303 逻辑：同款累加/规格/称重/沽清）</summary>
    public Ordering.AddProductAction AddProduct(DProduct product)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (!product.IsOnSale) return Ordering.AddProductAction.SoldOut;

        bool isWeighted = product.ProductType == 3 || product.WeightWay == 1;
        if (isWeighted) return Ordering.AddProductAction.NeedWeight;
        if (!string.IsNullOrWhiteSpace(product.SpecGroupName)) return Ordering.AddProductAction.NeedSpec;

        var existing = Lines.FirstOrDefault(l =>
            l.ProductId == product.Id && string.IsNullOrEmpty(l.SpecText) && !l.IsGift);
        if (existing != null) existing.Qty += 1;
        else Lines.Add(new OrderLineViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitName = product.UnitName,
            Price = product.Price,
            Qty = 1,
        });

        Recalc();
        RaisePropertyChanged(nameof(HasLines));
        return Ordering.AddProductAction.Added;
    }

    public void AddLine(OrderLineViewModel line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        var existing = Lines.FirstOrDefault(l =>
            l.ProductId == line.ProductId && l.SpecText == line.SpecText &&
            l.IsGift == line.IsGift && !l.IsWeighted);
        if (existing != null && !line.IsWeighted) existing.Qty += line.Qty;
        else Lines.Add(line);
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }

    public void RemoveLine(OrderLineViewModel line)
    {
        Lines.Remove(line);
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }

    private void Recalc()
    {
        Totals = OrderCalculator.CalcTotals(Lines.Select(l => l.ToInput()));
        foreach (var l in Lines) l.NotifyPropertyChanged(nameof(OrderLineViewModel.Amount));
    }

    // ---------- 提交与结账（先付后吃） ----------

    /// <summary>
    /// 点单完成（提交订单）。快餐必须先结账才能完成取号。
    /// </summary>
    public Task<SubmitResult> SubmitAsync(CancellationToken ct = default)
    {
        if (!HasTicket)
            return Task.FromResult(SubmitResult.Failed("请先取号"));
        if (!HasLines)
            return Task.FromResult(SubmitResult.Failed("明细为空，不能提交"));
        return Submit.SubmitAsync(DoSubmitAsync, ct);
    }

    private async Task<(bool ok, long orderId, bool conflict, string? error)> DoSubmitAsync(
        string clientOrderNo, CancellationToken ct)
    {
        try
        {
            var existing = await _orderRepo.GetByClientOrderNoAsync(clientOrderNo, ct).ConfigureAwait(false);
            if (existing != null) return (true, existing.Id, false, null);

            var order = BuildOrder(clientOrderNo);
            long id = await _orderRepo.InsertAsync(order, ct).ConfigureAwait(false);
            await _outboxRepo.EnqueueAsync(new OutboxItem
            {
                ClientOrderNo = clientOrderNo,
                ApiPath = "/api/v1/order/create",
                PayloadJson = $"{{\"clientOrderNo\":\"{clientOrderNo}\",\"takeNo\":\"{order.TakeNo}\",\"amountPay\":{order.AmountPay}}}",
            }, ct).ConfigureAwait(false);

            return (true, id, false, null);
        }
        catch (Exception ex)
        {
            return (false, 0, false, $"提交失败：{ex.Message}");
        }
    }

    private Order BuildOrder(string clientOrderNo)
    {
        var totals = Totals;
        var order = new Order
        {
            ClientOrderNo = clientOrderNo,
            DeskId = 0, // 快餐无桌台
            OrderType = OrderType,
            Status = OrderStatus.Pending,
            GuestCount = _ticket?.GuestCount ?? 1,
            TakeNo = _ticket?.Number ?? string.Empty,
            AmountTotal = totals.OriginalTotal,
            AmountDiscount = totals.LineDiscountTotal + totals.OrderDiscount + totals.WipeZeroAmount,
            AmountPay = totals.Payable,
            Remark = BuildRemark(),
            CreatedAt = DateTime.Now,
            SyncStatus = SyncStatus.Pending,
        };

        foreach (var l in Lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                SpecText = l.SpecText,
                Price = l.Price,
                Qty = l.IsWeighted ? l.WeightKg : l.Qty,
                Amount = l.Amount,
                DiscountAmount = l.LineDiscount,
                IsGift = l.IsGift,
                IsWeighted = l.IsWeighted,
                Remark = l.Remark,
            });
        }

        return order;
    }

    private string BuildRemark()
    {
        var parts = new List<string>();
        if (Takeaway.NeedsTableware && Takeaway.TablewareCount > 0)
            parts.Add($"餐具×{Takeaway.TablewareCount}");
        if (!string.IsNullOrWhiteSpace(Takeaway.Remark))
            parts.Add(Takeaway.Remark);
        if (OrderType == OrderType.Delivery)
        {
            // Domain 缺口：配送地址电话暂无字段，先拼进备注
            if (!string.IsNullOrWhiteSpace(Delivery.Address)) parts.Add($"配送地址：{Delivery.Address}");
            if (!string.IsNullOrWhiteSpace(Delivery.Phone)) parts.Add($"电话：{Delivery.Phone}");
        }
        return string.Join("；", parts);
    }

    /// <summary>标记已结账（收银完成）</summary>
    public void MarkSettled()
    {
        IsSettled = true;
        BlockWarning = null;
    }

    /// <summary>
    /// 尝试完成取号（叫下一位）。未结账时阻断并提示。
    /// </summary>
    public bool TryCompleteTicket(out string? blockReason)
    {
        blockReason = null;
        if (!IsSettled)
        {
            blockReason = "该单尚未结账，不能放走客人";
            BlockWarning = blockReason;
            return false;
        }

        if (_ticket != null)
            _tickets.CompleteCurrent();

        // 清理，准备下一单
        _ticket = null;
        Lines.Clear();
        IsSettled = false;
        BlockWarning = null;
        Submit.Reset();
        Recalc();
        RaisePropertyChanged(nameof(TicketNumber));
        RaisePropertyChanged(nameof(OrderTitleText));
        RaisePropertyChanged(nameof(HasTicket));
        RaisePropertyChanged(nameof(HasLines));
        return true;
    }
}
