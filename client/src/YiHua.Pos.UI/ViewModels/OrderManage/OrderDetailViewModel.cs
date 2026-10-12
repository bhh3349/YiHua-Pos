using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.ViewModels.OrderManage;

/// <summary>订单详情 ViewModel</summary>
public class OrderDetailViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;

    private Order? _order;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public OrderDetailViewModel(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Order? Order
    {
        get => _order;
        private set
        {
            SetProperty(ref _order, value);
            RaisePropertyChanged(nameof(Status));
            RaisePropertyChanged(nameof(CanRefund));
            RaisePropertyChanged(nameof(CanReverse));
            RaisePropertyChanged(nameof(CanReprint));
            RaisePropertyChanged(nameof(CanSkip));
        }
    }

    public OrderManageStatus Status =>
        Order == null ? OrderManageStatus.Pending : OrderListViewModel.MapStatus(Order.Status);

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

    /// <summary>操作按钮可见性：已结账订单的操作明显区别于待结</summary>
    public bool CanRefund => Status is OrderManageStatus.Pending or OrderManageStatus.Settled;
    public bool CanReverse => Status == OrderManageStatus.Settled;
    public bool CanReprint => Status != OrderManageStatus.Void;
    public bool CanSkip => Status == OrderManageStatus.Pending;

    public async Task LoadAsync(long orderId, CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Order = await _orderRepository.GetByIdAsync(orderId, ct).ConfigureAwait(false);
            if (Order == null)
                ErrorMessage = "订单不存在，可能已被删除";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"订单加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
