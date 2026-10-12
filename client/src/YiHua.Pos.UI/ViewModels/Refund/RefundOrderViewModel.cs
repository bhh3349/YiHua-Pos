using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>操作权限（供 UI 绑定：置灰 + 说明原因）</summary>
public class OperationAccess
{
    public bool Enabled { get; }
    public string? DenyReason { get; }

    public OperationAccess(bool enabled, string? denyReason = null)
    {
        Enabled = enabled;
        DenyReason = denyReason;
    }

    public static OperationAccess Allow() => new(true);
    public static OperationAccess Deny(string reason) => new(false, reason);
}

/// <summary>
/// 退款主界面 ViewModel：选择退菜 / 整单退 / 冲正。
/// 权限不足时按钮置灰并说明原因（不点后报错）。
/// </summary>
public class RefundOrderViewModel : ViewModelBase
{
    private readonly IRefundRepository _repo;
    private readonly IRefundPolicy _policy;
    private readonly KitchenNotifyService _kitchen;

    public Order Order { get; }
    public string OperatorName { get; }
    public string OperatorRole { get; }
    public long CurrentShiftId { get; }

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public decimal Refundable => RefundCalculator.CalcRefundable(Order);
    public decimal AlreadyRefunded => Order.AmountRefund;

    public RefundOrderViewModel(
        Order order,
        string operatorName,
        string operatorRole,
        long currentShiftId,
        IRefundRepository? repo = null,
        IRefundPolicy? policy = null,
        KitchenNotifyService? kitchen = null)
    {
        Order = order;
        OperatorName = operatorName;
        OperatorRole = operatorRole;
        CurrentShiftId = currentShiftId;
        _repo = repo ?? new InMemoryRefundRepository();
        _policy = policy ?? new DefaultRefundPolicy();
        _kitchen = kitchen ?? new KitchenNotifyService();
    }

    // --- 权限：置灰用 ---

    public OperationAccess ItemRefundAccess
    {
        get
        {
            var (allowed, deny) = _policy.CanPerform(RefundType.Item, OperatorRole);
            if (!allowed) return OperationAccess.Deny(deny!);
            var (ok, reason) = RefundStateMachine.CanRefundItem(Order.Status, false);
            return ok ? OperationAccess.Allow() : OperationAccess.Deny(reason!);
        }
    }

    public OperationAccess OrderRefundAccess
    {
        get
        {
            var (allowed, deny) = _policy.CanPerform(RefundType.Order, OperatorRole);
            if (!allowed) return OperationAccess.Deny(deny!);
            var (ok, reason) = RefundStateMachine.CanRefundOrder(Order.Status, Refundable);
            return ok ? OperationAccess.Allow() : OperationAccess.Deny(reason!);
        }
    }

    public OperationAccess VoidAccess
    {
        get
        {
            var (allowed, deny) = _policy.CanPerform(RefundType.Void, OperatorRole);
            if (!allowed) return OperationAccess.Deny(deny!);
            var (ok, reason) = RefundCalculator.ValidateVoid(Order);
            return ok ? OperationAccess.Allow() : OperationAccess.Deny(reason!);
        }
    }

    // --- 退菜 ---

    /// <summary>执行退菜：多选明细行</summary>
    public async Task<(bool Ok, string? Error)> RefundItemsAsync(
        IReadOnlyList<RefundableItem> selected,
        string reasonText,
        CancellationToken ct = default)
    {
        decimal total = Math.Round(selected.Sum(s => s.RefundAmount), 2, MidpointRounding.AwayFromZero);
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(Order, total);
        if (!ok)
            return (false, reason);

        var record = new RefundRecord
        {
            OrderId = Order.Id,
            OrderNo = Order.OrderNo ?? string.Empty,
            Type = RefundType.Item,
            Amount = total,
            Reason = reasonText,
            OperatorName = OperatorName,
            ShiftId = RefundCalculator.ResolveShiftId(Order, CurrentShiftId),
        };
        long recordId = await _repo.InsertAsync(record, ct).ConfigureAwait(false);

        // 联动后厨撤单（打印退菜单）
        var items = selected.Select(s => s.Item).ToList();
        var (printed, printError) = await _kitchen.NotifyCancelAsync(record, items, ct).ConfigureAwait(false);
        if (printed)
            await _repo.MarkPrintedAsync(recordId, ct).ConfigureAwait(false);

        // 更新订单累计
        Order.AmountRefund = Math.Round(Order.AmountRefund + total, 2, MidpointRounding.AwayFromZero);
        Order.Status = RefundCalculator.CalcStatusAfterRefund(Order, Order.AmountRefund);
        foreach (var s in selected)
            s.Item.IsRefunded = true;

        RaisePropertyChanged(nameof(Refundable));
        RaisePropertyChanged(nameof(AlreadyRefunded));

        return printError == null ? (true, null) : (true, $"退款成功，但{printError}");
    }

    // --- 整单退单 ---

    /// <summary>执行整单退款（支持部分退款、多次退款）</summary>
    public async Task<(bool Ok, string? Error)> RefundOrderAsync(
        decimal amount,
        string reasonText,
        string? authorizedBy,
        CancellationToken ct = default)
    {
        var (ok, reason) = RefundCalculator.ValidateOrderRefund(Order, amount);
        if (!ok)
            return (false, reason);

        var record = new RefundRecord
        {
            OrderId = Order.Id,
            OrderNo = Order.OrderNo ?? string.Empty,
            Type = RefundType.Order,
            Amount = amount,
            Reason = reasonText,
            OperatorName = OperatorName,
            AuthorizedBy = authorizedBy,
            ShiftId = RefundCalculator.ResolveShiftId(Order, CurrentShiftId),
        };
        await _repo.InsertAsync(record, ct).ConfigureAwait(false);

        Order.AmountRefund = Math.Round(Order.AmountRefund + amount, 2, MidpointRounding.AwayFromZero);
        Order.Status = RefundCalculator.CalcStatusAfterRefund(Order, Order.AmountRefund);

        RaisePropertyChanged(nameof(Refundable));
        RaisePropertyChanged(nameof(AlreadyRefunded));

        return (true, null);
    }

    /// <summary>是否需要店长授权</summary>
    public bool NeedsManagerAuth(decimal amount) =>
        _policy.RequiresManagerAuth(RefundType.Order, amount, OperatorRole);

    public decimal ManagerAuthThreshold => _policy.ManagerAuthThreshold;

    // --- 冲正 ---

    /// <summary>执行冲正：只对未结账有效，桌台释放由调用方处理（返回桌台 ID）</summary>
    public (bool Ok, string? Error, long DeskId) VoidOrder(string reasonText)
    {
        var (ok, reason) = RefundCalculator.ValidateVoid(Order);
        if (!ok)
            return (false, reason, 0);

        Order.Status = OrderStatus.Void;
        // 备注记录冲正原因与操作人（Domain 缺口：无结构化字段）
        Order.Remark = $"[冲正] {OperatorName} {DateTime.Now:HH:mm}：{reasonText}";

        return (true, null, Order.DeskId);
    }
}
