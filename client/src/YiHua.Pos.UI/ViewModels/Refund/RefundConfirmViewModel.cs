using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>退款确认 ViewModel：显示大字金额与具体影响</summary>
public class RefundConfirmViewModel : ViewModelBase
{
    public Order Order { get; }
    public RefundType Type { get; }

    private decimal _amount;
    /// <summary>本次退款金额</summary>
    public decimal Amount
    {
        get => _amount;
        set { SetProperty(ref _amount, value); RaisePropertyChanged(nameof(AfterStatusText)); RaisePropertyChanged(nameof(ImpactText)); }
    }

    public string ReasonText { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string? AuthorizedBy { get; set; }

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public decimal AlreadyRefunded => Order.AmountRefund;
    public decimal Received => Order.AmountReceived;
    public decimal Refundable => RefundCalculator.CalcRefundable(Order);

    /// <summary>大字金额文案：¥128 退回</summary>
    public string BigAmountText => $"¥{Amount:F2} 退回";

    /// <summary>退款后订单状态</summary>
    public OrderStatus AfterStatus =>
        RefundCalculator.CalcStatusAfterRefund(Order, Order.AmountRefund + Amount);

    public string AfterStatusText => AfterStatus switch
    {
        OrderStatus.Refunded => "订单将变为：已退单",
        _ => "订单将变为：部分退款（仍可继续营业）",
    };

    /// <summary>具体影响说明（不是简单的"确定吗"）</summary>
    public string ImpactText
    {
        get
        {
            var lines = new List<string>
            {
                $"本次退回 ¥{Amount:F2}",
                $"累计已退 ¥{AlreadyRefunded:F2}，还可退 ¥{Refundable:F2}",
                AfterStatusText,
            };
            if (Type == RefundType.Item)
                lines.Add("将通知后厨撤单并打印退菜单");
            if (!string.IsNullOrEmpty(AuthorizedBy))
                lines.Add($"店长授权：{AuthorizedBy}");
            return string.Join("\n", lines);
        }
    }

    public RefundConfirmViewModel(Order order, RefundType type, decimal amount)
    {
        Order = order;
        Type = type;
        _amount = amount;
    }

    public bool Validate()
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(Order, Amount);
        if (!ok)
        {
            Error = reason;
            return false;
        }
        Error = null;
        return true;
    }
}
