using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.ViewModels.OrderManage;

/// <summary>订单列表行模型</summary>
public class OrderRowModel
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string DeskName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public decimal AmountPay { get; set; }
    public string PayTypeText { get; set; } = string.Empty;
    public OrderManageStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 订单列表 ViewModel：默认当天，状态筛选 + 关键词搜索 + 排序 + 分页。
/// </summary>
public class OrderListViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;

    private DateTime _selectedDate = DateTime.Today;
    private OrderManageStatus? _statusFilter;
    private string _keyword = string.Empty;
    private bool _sortByAmountDesc;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public const int PageSize = 50;

    public OrderListViewModel(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
        Orders = new ObservableCollection<OrderRowModel>();
    }

    public ObservableCollection<OrderRowModel> Orders { get; }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set { if (SetProperty(ref _selectedDate, value.Date)) _ = LoadAsync(); }
    }

    public OrderManageStatus? StatusFilter
    {
        get => _statusFilter;
        set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); }
    }

    public string Keyword
    {
        get => _keyword;
        set { if (SetProperty(ref _keyword, value)) ApplyFilter(); }
    }

    /// <summary>排序：false=时间倒序（默认），true=金额从高到低</summary>
    public bool SortByAmountDesc
    {
        get => _sortByAmountDesc;
        set { if (SetProperty(ref _sortByAmountDesc, value)) ApplyFilter(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsEmpty => Orders.Count == 0 && !IsLoading && string.IsNullOrEmpty(ErrorMessage);
    public bool HasActiveFilter => StatusFilter != null || !string.IsNullOrWhiteSpace(Keyword);

    private List<Order> _allOrders = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var from = SelectedDate.Date;
            var to = from.AddDays(1);
            _allOrders = await _orderRepository.GetByDateRangeAsync(from, to, ct).ConfigureAwait(false);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"订单加载失败：{ex.Message}，请检查后重试";
        }
        finally
        {
            IsLoading = false;
            RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public void ClearFilter()
    {
        StatusFilter = null;
        Keyword = string.Empty;
    }

    private void ApplyFilter()
    {
        var query = _allOrders.AsEnumerable();

        if (StatusFilter != null)
        {
            var target = StatusFilter.Value;
            query = query.Where(o => MapStatus(o.Status) == target);
        }

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var kw = Keyword.Trim();
            query = query.Where(o =>
                o.OrderNo.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                o.AmountPay.ToString("F2").Contains(kw));
            // 注：桌号搜索需关联桌台，见 Domain 缺口清单
        }

        query = SortByAmountDesc
            ? query.OrderByDescending(o => o.AmountPay).ThenByDescending(o => o.CreatedAt)
            : query.OrderByDescending(o => o.CreatedAt);

        // 分页：取前 PageSize 条（滚动加载由视图触发 LoadMore）
        var page = query.Take(PageSize).ToList();

        Orders.Clear();
        foreach (var o in page)
        {
            Orders.Add(new OrderRowModel
            {
                Id = o.Id,
                OrderNo = o.OrderNo,
                GuestCount = o.GuestCount,
                AmountPay = o.AmountPay,
                Status = MapStatus(o.Status),
                CreatedAt = o.CreatedAt,
                PayTypeText = o.Payments.FirstOrDefault()?.PayType.ToString() ?? string.Empty,
            });
        }
        RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(HasActiveFilter));
    }

    /// <summary>Domain OrderStatus → UI 完整状态（缺口状态暂映射为最接近的）</summary>
    public static OrderManageStatus MapStatus(OrderStatus s) => s switch
    {
        OrderStatus.Pending => OrderManageStatus.Pending,
        OrderStatus.Settled => OrderManageStatus.Settled,
        OrderStatus.Refunded => OrderManageStatus.Refunded,
        OrderStatus.Void => OrderManageStatus.Void,
        _ => OrderManageStatus.Pending,
    };
}
