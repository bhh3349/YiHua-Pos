using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>退菜明细行（带选中状态）</summary>
public class RefundableItem : ViewModelBase
{
    public OrderItem Item { get; }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private decimal _refundAmount;
    /// <summary>本次退该行的金额（默认整行）</summary>
    public decimal RefundAmount
    {
        get => _refundAmount;
        set => SetProperty(ref _refundAmount, value);
    }

    public decimal LineAmount => Math.Round(Item.Price * Item.Qty, 2, MidpointRounding.AwayFromZero);
    public bool AlreadyRefunded => Item.IsRefunded;

    public RefundableItem(OrderItem item)
    {
        Item = item;
        _refundAmount = LineAmount;
    }
}

/// <summary>退菜多选 ViewModel</summary>
public class RefundItemSelectViewModel : ViewModelBase
{
    public Order Order { get; }
    public ObservableCollection<RefundableItem> Items { get; } = new();

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public RefundItemSelectViewModel(Order order)
    {
        Order = order;
        foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
            Items.Add(new RefundableItem(item));

        foreach (var ri in Items)
            ri.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(RefundableItem.Selected) or nameof(RefundableItem.RefundAmount))
                    RaisePropertyChanged(nameof(TotalSelected));
            };
    }

    /// <summary>已选明细行</summary>
    public IReadOnlyList<RefundableItem> SelectedItems =>
        Items.Where(i => i.Selected).ToList();

    /// <summary>已选退菜总金额</summary>
    public decimal TotalSelected =>
        Math.Round(SelectedItems.Sum(i => i.RefundAmount), 2, MidpointRounding.AwayFromZero);

    /// <summary>校验全部已选行</summary>
    public bool ValidateAll()
    {
        if (!SelectedItems.Any())
        {
            Error = "请至少选择一行要退的菜";
            return false;
        }
        foreach (var sel in SelectedItems)
        {
            var (ok, reason) = RefundCalculator.ValidateItemRefund(sel.Item, sel.RefundAmount);
            if (!ok)
            {
                Error = reason;
                return false;
            }
        }
        Error = null;
        return true;
    }
}
