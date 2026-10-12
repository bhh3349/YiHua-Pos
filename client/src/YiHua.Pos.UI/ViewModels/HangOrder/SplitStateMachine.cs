using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>
/// 拆单状态机（纯逻辑）。
/// 管理"选了哪些菜→拆出多少→剩余多少→订单什么状态"的完整流转。
/// </summary>
public sealed class SplitStateMachine : ViewModelBase
{
    private readonly Order _order;
    private readonly HashSet<long> _alreadySettled;
    private readonly HashSet<long> _selected = new();

    public SplitStateMachine(Order order, IEnumerable<long>? alreadySettledIds = null)
    {
        _order = order ?? throw new ArgumentNullException(nameof(order));
        _alreadySettled = new HashSet<long>(alreadySettledIds ?? Enumerable.Empty<long>());
        Refresh();
    }

    public Order Order => _order;

    /// <summary>可拆的明细（未结过的）</summary>
    public IReadOnlyList<OrderItem> AvailableItems => _order.Items
        .Where(i => !_alreadySettled.Contains(i.Id) && !i.IsGift && !i.IsRefunded)
        .ToList();

    /// <summary>已选明细ID</summary>
    public IReadOnlySet<long> SelectedIds => _selected;

    /// <summary>拆出金额（实时）</summary>
    public decimal SplitAmount { get; private set; }

    /// <summary>剩余金额（实时）</summary>
    public decimal RemainingAmount { get; private set; }

    /// <summary>拆单后订单状态</summary>
    public OrderStatus StatusAfterSplit { get; private set; }

    /// <summary>当前选择是否合法</summary>
    public bool IsValid { get; private set; }

    public string? ValidationReason { get; private set; }

    public void ToggleSelect(long itemId)
    {
        if (!_selected.Add(itemId))
            _selected.Remove(itemId);
        Refresh();
    }

    public void SelectAll()
    {
        _selected.Clear();
        foreach (var i in AvailableItems) _selected.Add(i.Id);
        Refresh();
    }

    public void ClearSelection()
    {
        _selected.Clear();
        Refresh();
    }

    /// <summary>确认拆单：返回本次结账的明细ID列表（调用方负责落库）</summary>
    public IReadOnlyList<long> Confirm(out string? reason)
    {
        reason = null;
        var ids = _selected.ToList();
        if (!HangOrderCalculator.ValidateSplit(_order, ids, _alreadySettled, out reason))
            return Array.Empty<long>();
        return ids;
    }

    private void Refresh()
    {
        var ids = _selected.ToList();
        var items = _order.Items.Where(i => ids.Contains(i.Id)).ToList();

        SplitAmount = HangOrderCalculator.CalcSplitAmount(items);
        RemainingAmount = HangOrderCalculator.CalcRemaining(_order, _alreadySettled, ids);

        var allSettled = new HashSet<long>(_alreadySettled);
        foreach (var id in ids) allSettled.Add(id);
        StatusAfterSplit = HangOrderCalculator.CalcStatusAfterSplit(_order, allSettled);

        IsValid = HangOrderCalculator.ValidateSplit(_order, ids, _alreadySettled, out string? reason);
        ValidationReason = reason;

        RaisePropertyChanged(nameof(SelectedIds));
        RaisePropertyChanged(nameof(SplitAmount));
        RaisePropertyChanged(nameof(RemainingAmount));
        RaisePropertyChanged(nameof(StatusAfterSplit));
        RaisePropertyChanged(nameof(IsValid));
        RaisePropertyChanged(nameof(ValidationReason));
        RaisePropertyChanged(nameof(AvailableItems));
    }
}
