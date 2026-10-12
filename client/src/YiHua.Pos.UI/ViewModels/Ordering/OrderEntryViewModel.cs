using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Ordering;

namespace YiHua.Pos.UI.ViewModels.Ordering;

// 别名必须放在 namespace 内部：文件层级的 using 属于全局作用域，
// 而 YiHua.Pos.UI.ViewModels 下有 Desk / Product 两个子命名空间，
// 名字查找时更近的作用域优先，全局别名根本轮不上。
using Desk = YiHua.Pos.Domain.Entities.Desk;
using Product = YiHua.Pos.Domain.Entities.Product;

/// <summary>
/// 加菜动作：点商品后 UI 该做什么。
/// </summary>
public enum AddProductAction
{
    /// <summary>直接加到明细</summary>
    Added,
    /// <summary>需要选规格</summary>
    NeedSpec,
    /// <summary>需要输入重量</summary>
    NeedWeight,
    /// <summary>已沽清，不可点</summary>
    SoldOut,
}

/// <summary>
/// 正餐点单主 ViewModel。
/// 左商品区 + 右明细区；金额计算走 <see cref="OrderCalculator"/>。
/// </summary>
public sealed class OrderEntryViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IOutboxRepository _outboxRepo;

    private long? _selectedCategoryId;
    private OrderTotals _totals = new();
    private decimal _orderDiscountRate = 1m;
    private bool _wipeZeroEnabled;
    private string _orderRemark = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    public OrderEntryViewModel(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        IOrderRepository orderRepo,
        IOutboxRepository outboxRepo,
        HangOrderStore? hangStore = null,
        OperatorRole role = OperatorRole.Cashier,
        Desk? desk = null)
    {
        _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
        _categoryRepo = categoryRepo ?? throw new ArgumentNullException(nameof(categoryRepo));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));
        HangStore = hangStore ?? new HangOrderStore();
        Role = role;
        Desk = desk;
        Submit = new SubmitStateMachine();
    }

    public Desk? Desk { get; }
    public OperatorRole Role { get; }
    public HangOrderStore HangStore { get; }
    public SubmitStateMachine Submit { get; }

    /// <summary>桌台显示名（如 "A08"）</summary>
    public string DeskName => Desk?.Name ?? "外带";

    /// <summary>已开台时长文本（如 "已开 25 分"），未开台返回空</summary>
    public string OpenDurationText
    {
        get
        {
            if (Desk?.OpenTime == null) return string.Empty;
            int minutes = (int)(DateTime.Now - Desk.OpenTime.Value).TotalMinutes;
            return minutes < 60 ? $"已开 {minutes} 分" : $"已开 {minutes / 60} 小时 {minutes % 60} 分";
        }
    }

    public ObservableCollection<CategoryTabViewModel> Categories { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<OrderLineViewModel> Lines { get; } = new();

    private OrderLineViewModel? _selectedLine;

    /// <summary>选中的明细行（退菜/备注目标）</summary>
    public OrderLineViewModel? SelectedLine
    {
        get => _selectedLine;
        set => SetProperty(ref _selectedLine, value);
    }

    /// <summary>是否有整单折扣（界面显示用）</summary>
    public bool HasDiscount => Totals.OrderDiscount > 0m;

    /// <summary>是否有抹零（界面显示用）</summary>
    public bool HasWipeZero => Totals.WipeZeroAmount > 0m;

    public long? SelectedCategoryId
    {
        get => _selectedCategoryId;
        set
        {
            if (SetProperty(ref _selectedCategoryId, value))
            {
                foreach (var c in Categories)
                    c.IsSelected = c.Id == value;
                _ = FilterProductsAsync();
            }
        }
    }

    /// <summary>点选分类标签</summary>
    public void SelectCategory(CategoryTabViewModel category)
    {
        SelectedCategoryId = category?.Id;
    }

    public OrderTotals Totals
    {
        get => _totals;
        private set => SetProperty(ref _totals, value);
    }

    /// <summary>整单折扣率（1 = 不打折）</summary>
    public decimal OrderDiscountRate
    {
        get => _orderDiscountRate;
        set { if (SetProperty(ref _orderDiscountRate, value)) Recalc(); }
    }

    public bool WipeZeroEnabled
    {
        get => _wipeZeroEnabled;
        set { if (SetProperty(ref _wipeZeroEnabled, value)) Recalc(); }
    }

    public string OrderRemark
    {
        get => _orderRemark;
        set => SetProperty(ref _orderRemark, value);
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

    public bool IsManager => Role == OperatorRole.Manager;
    public bool HasLines => Lines.Count > 0;

    // ---------- 商品加载 ----------

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var cats = await _categoryRepo.GetAllAsync(ct).ConfigureAwait(false);
            var prods = await _productRepo.GetAllAsync(ct).ConfigureAwait(false);

            Categories.Clear();
            foreach (var c in cats.OrderBy(c => c.Sort)) Categories.Add(new CategoryTabViewModel(c));

            _allProducts = prods.OrderBy(p => p.Sort).ToList();
            await FilterProductsAsync().ConfigureAwait(false);
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

    private List<Product> _allProducts = new();

    private Task FilterProductsAsync()
    {
        Products.Clear();
        var list = _selectedCategoryId == null
            ? _allProducts
            : _allProducts.Where(p => p.CategoryId == _selectedCategoryId.Value);
        foreach (var p in list) Products.Add(p);
        return Task.CompletedTask;
    }

    // ---------- 加菜 ----------

    /// <summary>
    /// 点商品。返回 UI 该做的动作。
    /// 同款无规格商品直接数量 +1；有规格/称重则返回对应动作由 UI 弹窗处理。
    /// </summary>
    public AddProductAction AddProduct(Product product)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));

        // 沽清：DataStatus != 1 不可点
        if (!product.IsOnSale)
            return AddProductAction.SoldOut;

        bool isWeighted = product.ProductType == 3 || product.WeightWay == 1;
        if (isWeighted)
            return AddProductAction.NeedWeight;

        bool hasSpec = !string.IsNullOrWhiteSpace(product.SpecGroupName);
        if (hasSpec)
            return AddProductAction.NeedSpec;

        // 无规格：找同款（同商品+同规格+未退）直接 +1
        var existing = Lines.FirstOrDefault(l =>
            l.ProductId == product.Id &&
            string.IsNullOrEmpty(l.SpecText) &&
            !l.IsGift && !l.IsRefunded);
        if (existing != null)
        {
            existing.Qty += 1;
        }
        else
        {
            Lines.Add(new OrderLineViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitName = product.UnitName,
                Price = product.Price,
                Qty = 1,
            });
        }

        Recalc();
        RaisePropertyChanged(nameof(HasLines));
        return AddProductAction.Added;
    }

    /// <summary>
    /// 规格/称重确认后，真正加一行。
    /// 同款同规格累加；不同规格另起一行。
    /// </summary>
    public void AddLine(OrderLineViewModel line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));

        var existing = Lines.FirstOrDefault(l =>
            l.ProductId == line.ProductId &&
            l.SpecText == line.SpecText &&
            l.IsGift == line.IsGift &&
            !l.IsRefunded &&
            !l.IsWeighted);
        if (existing != null && !line.IsWeighted)
        {
            existing.Qty += line.Qty;
        }
        else
        {
            Lines.Add(line);
        }

        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }

    public void IncrementLine(OrderLineViewModel line)
    {
        if (line.IsWeighted) return; // 称重商品改重量，不走步进
        line.Qty += 1;
        Recalc();
    }

    public void DecrementLine(OrderLineViewModel line)
    {
        if (line.IsWeighted) return;
        if (line.Qty <= 1)
            Lines.Remove(line);
        else
            line.Qty -= 1;
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }

    public void RemoveLine(OrderLineViewModel line)
    {
        Lines.Remove(line);
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }

    // ---------- 改价 / 折扣 / 赠菜（仅店长） ----------

    /// <summary>改单价（仅店长）。返回 false 表示无权限。</summary>
    public bool TryEditPrice(OrderLineViewModel line, decimal newPrice, out string? denyReason)
    {
        denyReason = null;
        if (!IsManager)
        {
            denyReason = "改单价需要店长权限";
            return false;
        }
        if (newPrice < 0m)
        {
            denyReason = "单价不能为负";
            return false;
        }
        line.Price = newPrice;
        Recalc();
        return true;
    }

    /// <summary>行折扣（仅店长，折扣额）</summary>
    public bool TrySetLineDiscount(OrderLineViewModel line, decimal discount, out string? denyReason)
    {
        denyReason = null;
        if (!IsManager)
        {
            denyReason = "行折扣需要店长权限";
            return false;
        }
        line.LineDiscount = Math.Max(0m, discount);
        Recalc();
        return true;
    }

    /// <summary>整单折扣率（仅店长，如 0.9 = 九折）</summary>
    public bool TrySetOrderDiscountRate(decimal rate, out string? denyReason)
    {
        denyReason = null;
        if (!IsManager)
        {
            denyReason = "整单折扣需要店长权限";
            return false;
        }
        if (rate <= 0m || rate > 1m)
        {
            denyReason = "折扣率必须在 (0, 1] 之间";
            return false;
        }
        OrderDiscountRate = rate;
        return true;
    }

    /// <summary>赠菜切换（仅店长）</summary>
    public bool TryToggleGift(OrderLineViewModel line, out string? denyReason)
    {
        denyReason = null;
        if (!IsManager)
        {
            denyReason = "赠菜需要店长权限";
            return false;
        }
        line.IsGift = !line.IsGift;
        Recalc();
        return true;
    }

    // ---------- 备注 / 退菜 ----------

    public void SetLineRemark(OrderLineViewModel line, string remark)
    {
        line.Remark = remark?.Length > 30 ? remark.Substring(0, 30) : (remark ?? string.Empty);
    }

    /// <summary>
    /// 退菜：未提交的行直接移除；已提交的行标记退菜并记录原因。
    /// 结账后（订单非 Pending）的退菜由订单管理模块处理，这里拒绝。
    /// </summary>
    public bool TryRefundLine(OrderLineViewModel line, string reason, bool isSubmitted, out string? denyReason)
    {
        denyReason = null;
        if (string.IsNullOrWhiteSpace(reason))
        {
            denyReason = "退菜必须填写原因";
            return false;
        }
        if (!isSubmitted)
        {
            Lines.Remove(line);
        }
        else
        {
            line.IsRefunded = true;
            line.Remark = $"退菜原因：{reason}";
        }
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
        return true;
    }

    // ---------- 挂单 ----------

    /// <summary>挂单：快照当前明细，清空界面</summary>
    public string HangCurrentOrder()
    {
        var snapshot = new HangOrderSnapshot
        {
            DeskId = Desk?.Id ?? 0,
            DeskName = DeskName,
            GuestCount = Desk?.GuestCount ?? 0,
            Lines = Lines.Select(l => l.ToInput()).ToList(),
            Remark = OrderRemark,
        };
        string hangId = HangStore.Hang(snapshot);
        Lines.Clear();
        OrderRemark = string.Empty;
        OrderDiscountRate = 1m;
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
        return hangId;
    }

    /// <summary>取单：恢复挂单到当前界面</summary>
    public bool RestoreHangOrder(string hangId)
    {
        var snapshot = HangStore.Take(hangId);
        if (snapshot == null) return false;

        Lines.Clear();
        foreach (var input in snapshot.Lines)
        {
            Lines.Add(new OrderLineViewModel
            {
                Qty = input.Qty,
                Price = input.Price,
                LineDiscount = input.LineDiscount,
                IsGift = input.IsGift,
            });
        }
        OrderRemark = snapshot.Remark;
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
        return true;
    }

    // ---------- 金额 ----------

    private void Recalc()
    {
        Totals = OrderCalculator.CalcTotals(
            Lines.Select(l => l.ToInput()),
            OrderDiscountRate,
            WipeZeroEnabled);
        // 刷新每行的显示金额
        foreach (var l in Lines) l.NotifyPropertyChanged(nameof(OrderLineViewModel.Amount));
        RaisePropertyChanged(nameof(HasDiscount));
        RaisePropertyChanged(nameof(HasWipeZero));
    }

    // ---------- 提交下单 ----------

    /// <summary>
    /// 提交下单（防重/幂等）。返回提交结果。
    /// </summary>
    public Task<SubmitResult> SubmitAsync(CancellationToken ct = default)
    {
        if (!HasLines)
            return Task.FromResult(SubmitResult.Failed("明细为空，不能提交"));

        return Submit.SubmitAsync(DoSubmitAsync, ct);
    }

    private async Task<(bool ok, long orderId, bool conflict, string? error)> DoSubmitAsync(
        string clientOrderNo, CancellationToken ct)
    {
        try
        {
            // 幂等检查：该 clientOrderNo 是否已存在（重试/重复提交）
            var existing = await _orderRepo.GetByClientOrderNoAsync(clientOrderNo, ct).ConfigureAwait(false);
            if (existing != null)
                return (true, existing.Id, false, null);

            // 冲突检查：桌台订单是否已被他人结账
            if (Desk != null && Desk.Id != 0)
            {
                var deskOrders = await _orderRepo.GetByDeskIdAsync(Desk.Id, ct).ConfigureAwait(false);
                var active = deskOrders.FirstOrDefault(o =>
                    o.Status != OrderStatus.Settled &&
                    o.Status != OrderStatus.Void &&
                    o.Status != OrderStatus.Refunded);
                if (active != null && active.Status != OrderStatus.Pending)
                    return (false, 0, true, $"桌台 {Desk.Name} 的订单已被{DescribeStatus(active.Status)}，请刷新后重试");
            }

            var order = BuildOrder(clientOrderNo);
            long id = await _orderRepo.InsertAsync(order, ct).ConfigureAwait(false);

            // 写 outbox（断网补传）
            await _outboxRepo.EnqueueAsync(new OutboxItem
            {
                ClientOrderNo = clientOrderNo,
                ApiPath = "/api/v1/order/create",
                PayloadJson = $"{{\"clientOrderNo\":\"{clientOrderNo}\",\"deskId\":{order.DeskId},\"amountPay\":{order.AmountPay}}}",
            }, ct).ConfigureAwait(false);

            return (true, id, false, null);
        }
        catch (Exception ex)
        {
            return (false, 0, false, $"提交失败：{ex.Message}");
        }
    }

    private static string DescribeStatus(OrderStatus s) => s switch
    {
        OrderStatus.Settled => "结账",
        OrderStatus.Refunded => "退单",
        OrderStatus.Void => "作废",
        _ => "处理",
    };

    private Order BuildOrder(string clientOrderNo)
    {
        var totals = Totals;
        var order = new Order
        {
            ClientOrderNo = clientOrderNo,
            DeskId = Desk?.Id ?? 0,
            OrderType = OrderType.DineIn,
            Status = OrderStatus.Pending,
            GuestCount = Desk?.GuestCount ?? 0,
            AmountTotal = totals.OriginalTotal,
            AmountDiscount = totals.LineDiscountTotal + totals.OrderDiscount + totals.WipeZeroAmount,
            AmountPay = totals.Payable,
            Remark = OrderRemark,
            CreatedAt = DateTime.Now,
            SyncStatus = SyncStatus.Pending,
        };

        foreach (var l in Lines.Where(l => !l.IsRefunded))
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

    /// <summary>提交成功后清理（继续开台用）</summary>
    public void ClearAfterSubmit()
    {
        Lines.Clear();
        OrderRemark = string.Empty;
        OrderDiscountRate = 1m;
        WipeZeroEnabled = false;
        Submit.Reset();
        Recalc();
        RaisePropertyChanged(nameof(HasLines));
    }
}
