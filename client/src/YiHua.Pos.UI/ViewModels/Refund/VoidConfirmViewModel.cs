using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>冲正确认 ViewModel：只对未结账有效，作废后释放桌台</summary>
public class VoidConfirmViewModel : ViewModelBase
{
    public Order Order { get; }

    public string ReasonText { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public bool CanVoid => RefundCalculator.ValidateVoid(Order).Ok;

    public string BlockReason => RefundCalculator.ValidateVoid(Order).Reason ?? string.Empty;

    /// <summary>影响说明：桌台释放是重点</summary>
    public string ImpactText =>
        $"作废后不产生退款\n" +
        $"桌台 {Order.DeskId} 将释放为空闲\n" +
        $"作废单在订单管理中灰显但可见";

    public VoidConfirmViewModel(Order order) => Order = order;

    public bool Validate()
    {
        var (ok, reason) = RefundCalculator.ValidateVoid(Order);
        if (!ok)
        {
            Error = reason;
            return false;
        }
        Error = null;
        return true;
    }
}
