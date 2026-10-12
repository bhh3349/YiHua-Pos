using YiHua.Pos.UI.Services.Ordering;

namespace YiHua.Pos.UI.ViewModels.Ordering;

/// <summary>
/// 点单明细行 ViewModel（界面正在编辑的行，尚未提交）。
/// </summary>
public sealed class OrderLineViewModel : ViewModelBase
{
    private decimal _qty = 1m;
    private decimal _price;
    private decimal _lineDiscount;
    private bool _isGift;
    private bool _isRefunded;
    private string _specText = string.Empty;
    private string _remark = string.Empty;
    private decimal _weightKg;

    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;

    /// <summary>是否称重商品</summary>
    public bool IsWeighted { get; set; }

    public decimal Qty
    {
        get => _qty;
        set { if (SetProperty(ref _qty, value)) RaisePropertyChanged(nameof(Amount)); }
    }

    public decimal Price
    {
        get => _price;
        set { if (SetProperty(ref _price, value)) RaisePropertyChanged(nameof(Amount)); }
    }

    public decimal LineDiscount
    {
        get => _lineDiscount;
        set { if (SetProperty(ref _lineDiscount, value)) RaisePropertyChanged(nameof(Amount)); }
    }

    public bool IsGift
    {
        get => _isGift;
        set
        {
            if (SetProperty(ref _isGift, value))
            {
                RaisePropertyChanged(nameof(Amount));
                RaisePropertyChanged(nameof(DisplayAmount));
            }
        }
    }

    public bool IsRefunded
    {
        get => _isRefunded;
        set { if (SetProperty(ref _isRefunded, value)) RaisePropertyChanged(nameof(Amount)); }
    }

    public string SpecText
    {
        get => _specText;
        set => SetProperty(ref _specText, value);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    /// <summary>称重商品的重量（kg）</summary>
    public decimal WeightKg
    {
        get => _weightKg;
        set
        {
            if (SetProperty(ref _weightKg, value))
            {
                RaisePropertyChanged(nameof(Amount));
                RaisePropertyChanged(nameof(DisplayQty));
            }
        }
    }

    /// <summary>行金额（计算值）</summary>
    public decimal Amount => OrderCalculator.CalcLineAmount(ToInput());

    /// <summary>界面显示的金额（赠菜显示 0）</summary>
    public decimal DisplayAmount => IsGift ? 0m : Amount;

    /// <summary>界面显示的数量（称重商品显示重量）</summary>
    public string DisplayQty => IsWeighted ? $"{WeightKg:F2}kg" : Qty.ToString("G29");

    public OrderLineInput ToInput() => new()
    {
        Qty = IsWeighted ? WeightKg : Qty,
        Price = Price,
        LineDiscount = LineDiscount,
        IsGift = IsGift,
        IsRefunded = IsRefunded,
    };
}
