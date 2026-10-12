using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.ViewModels.OrderManage;

/// <summary>退菜原因</summary>
public enum RefundReason
{
    DontWant,      // 不要了
    WrongDish,     // 上错了
    CustomerLeft,  // 客人不要了
    QualityIssue,  // 质量问题
    Other,         // 其他
}

/// <summary>
/// 退菜 ViewModel：支持整道退/部分退，必填原因；
/// 未结账→直接改明细；已结账→生成退菜单（不改原单）。
/// </summary>
public class ReturnDishViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;

    private decimal _refundQty;
    private RefundReason _reason;
    private string _reasonNote = string.Empty;
    private string _errorMessage = string.Empty;

    public ReturnDishViewModel(IOrderRepository orderRepository, Order order, OrderItem item)
    {
        _orderRepository = orderRepository;
        Order = order;
        Item = item;
        _refundQty = item.Qty;
    }

    public Order Order { get; }
    public OrderItem Item { get; }
    public OrderManageStatus Status => OrderListViewModel.MapStatus(Order.Status);

    /// <summary>退菜数量（不能超过已点数量）</summary>
    public decimal RefundQty
    {
        get => _refundQty;
        set => SetProperty(ref _refundQty, value);
    }

    public RefundReason Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string ReasonNote
    {
        get => _reasonNote;
        set => SetProperty(ref _reasonNote, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>是否整道退</summary>
    public bool IsFullRefund => RefundQty >= Item.Qty;

    public event EventHandler? Refunded;

    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        var check = OrderStatusTransitions.TryRefund(Status, RefundQty, Item.Qty);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        if (Status == OrderManageStatus.Pending)
        {
            // 未结账：直接改明细，重算应收
            Item.Qty -= RefundQty;
            Item.Amount = Item.Price * Item.Qty;
            if (Item.Qty <= 0)
                Item.IsRefunded = true;
            Recalculate(Order);
            await _orderRepository.UpdateAsync(Order, ct).ConfigureAwait(false);
        }
        else
        {
            // 已结账：生成退菜单，不改原单（退菜单实体见 Domain 缺口清单）
            // 此处标记明细为部分退，实际退单据由服务端生成
            Item.IsRefunded = true;
            Order.AmountRefund += Item.Price * RefundQty;
            await _orderRepository.UpdateAsync(Order, ct).ConfigureAwait(false);
        }

        Refunded?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static void Recalculate(Order order)
    {
        order.AmountTotal = order.Items.Where(i => !i.IsRefunded).Sum(i => i.Amount);
        order.AmountPay = order.AmountTotal - order.AmountDiscount;
    }

    public static string DescribeReason(RefundReason r) => r switch
    {
        RefundReason.DontWant => "不要了",
        RefundReason.WrongDish => "上错了",
        RefundReason.CustomerLeft => "客人不要了",
        RefundReason.QualityIssue => "质量问题",
        _ => "其他",
    };
}

/// <summary>
/// 反结账 ViewModel：生成负数单（冲正），原单标记已冲正，原单完整保留。
/// 仅店长可操作。
/// </summary>
public class ReverseSettleViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;

    private string _reason = string.Empty;
    private string _errorMessage = string.Empty;

    public ReverseSettleViewModel(IOrderRepository orderRepository, Order order)
    {
        _orderRepository = orderRepository;
        Order = order;
    }

    public Order Order { get; }
    public OrderManageStatus Status => OrderListViewModel.MapStatus(Order.Status);

    /// <summary>反结账原因（必填）</summary>
    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>确认弹窗显示的具体影响</summary>
    public string ImpactText =>
        $"反结账后订单 {Order.OrderNo} 将标记为已冲正，金额 -{Order.AmountPay:F2}，" +
        $"桌台恢复占用状态（客人可能还在吃）";

    public event EventHandler<Order>? Reversed;

    public async Task<bool> ConfirmAsync(string operatorRole, CancellationToken ct = default)
    {
        if (operatorRole != "店长")
        {
            ErrorMessage = "反结账仅店长可操作";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "请填写反结账原因";
            return false;
        }

        var check = OrderStatusTransitions.TryReverse(Status);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        // 生成负数单（冲正单实体见 Domain 缺口清单，此处用 Order 表达）
        var reversal = new Order
        {
            OrderNo = $"R-{Order.OrderNo}",
            ClientOrderNo = Guid.NewGuid().ToString("N"),
            DeskId = Order.DeskId,
            Status = OrderStatus.Void, // 占位：实际应为 Reversed，见缺口清单
            AmountTotal = -Order.AmountTotal,
            AmountPay = -Order.AmountPay,
            Remark = $"冲正原单 {Order.OrderNo}：{Reason.Trim()}",
            CreatedAt = DateTime.Now,
        };
        await _orderRepository.InsertAsync(reversal, ct).ConfigureAwait(false);

        // 原单标记（实际应为 Reversed 状态，见缺口清单）
        Order.Remark += $" [已冲正：{Reason.Trim()}]";
        await _orderRepository.UpdateAsync(Order, ct).ConfigureAwait(false);

        Reversed?.Invoke(this, reversal);
        return true;
    }
}

/// <summary>重打记录（Domain 暂无实体，UI 层模型；见缺口清单）</summary>
public class ReprintRecord
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string ReprintType { get; set; } = string.Empty; // 小票/后厨单/交班单
    public string OperatorName { get; set; } = string.Empty;
    public DateTime ReprintTime { get; set; }
}

/// <summary>
/// 重打 ViewModel：不改变订单状态，记录操作人/时间。
/// </summary>
public class ReprintViewModel : ViewModelBase
{
    private string _reprintType = "小票";
    private string _errorMessage = string.Empty;

    public ReprintViewModel(Order order, string operatorName)
    {
        Order = order;
        OperatorName = operatorName;
    }

    public Order Order { get; }
    public string OperatorName { get; }
    public OrderManageStatus Status => OrderListViewModel.MapStatus(Order.Status);

    public string ReprintType
    {
        get => _reprintType;
        set => SetProperty(ref _reprintType, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler<ReprintRecord>? Reprinted;

    public bool Confirm()
    {
        var check = OrderStatusTransitions.TryReprint(Status);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        var record = new ReprintRecord
        {
            OrderId = Order.Id,
            OrderNo = Order.OrderNo,
            ReprintType = ReprintType,
            OperatorName = OperatorName,
            ReprintTime = DateTime.Now,
        };
        Reprinted?.Invoke(this, record);
        return true;
    }
}

/// <summary>
/// 跳单 ViewModel：待结→已跳单，可恢复。
/// </summary>
public class SkipOrderViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;

    private string _reason = string.Empty;
    private string _errorMessage = string.Empty;

    public SkipOrderViewModel(IOrderRepository orderRepository, Order order)
    {
        _orderRepository = orderRepository;
        Order = order;
    }

    public Order Order { get; }
    public OrderManageStatus Status => OrderListViewModel.MapStatus(Order.Status);

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler? Skipped;
    public event EventHandler? Restored;

    public async Task<bool> SkipAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "请填写跳单原因";
            return false;
        }
        var check = OrderStatusTransitions.TrySkip(Status);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        // 实际应为 Skipped 状态，见缺口清单；此处用备注标记
        Order.Remark += $" [已跳单：{Reason.Trim()}]";
        await _orderRepository.UpdateAsync(Order, ct).ConfigureAwait(false);
        Skipped?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> RestoreAsync(CancellationToken ct = default)
    {
        // 实际应检查 Skipped 状态，见缺口清单
        Order.Remark += " [跳单已恢复]";
        await _orderRepository.UpdateAsync(Order, ct).ConfigureAwait(false);
        Restored?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
