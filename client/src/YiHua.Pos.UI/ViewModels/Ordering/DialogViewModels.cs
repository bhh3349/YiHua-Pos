using System.Collections.ObjectModel;

namespace YiHua.Pos.UI.ViewModels.Ordering;

/// <summary>规格选项</summary>
public sealed class SpecOption : ViewModelBase
{
    private bool _isSelected;
    public string Group { get; set; } = string.Empty;   // 如 "大小"
    public string Value { get; set; } = string.Empty;   // 如 "大杯"
    public decimal PriceDelta { get; set; }             // 加价

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// 规格选择 ViewModel。
/// 触控友好：点选后自动收起（每组选完即确认，不强制点确定）。
/// </summary>
public sealed class SpecSelectViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<SpecSelectResult?> _tcs = new();

    public SpecSelectViewModel(string productName, IEnumerable<SpecOption> options)
    {
        ProductName = productName;
        foreach (var o in options) Options.Add(o);
    }

    public string ProductName { get; }
    public ObservableCollection<SpecOption> Options { get; } = new();

    /// <summary>分组名列表</summary>
    public IReadOnlyList<string> Groups => Options.Select(o => o.Group).Distinct().ToList();

    public Task<SpecSelectResult?> WaitResultAsync() => _tcs.Task;

    /// <summary>点选某个选项：同组单选，全部组选完后自动确认</summary>
    public void Select(SpecOption option)
    {
        foreach (var o in Options.Where(o => o.Group == option.Group))
            o.IsSelected = false;
        option.IsSelected = true;

        // 每组都有选中 → 自动收起确认
        if (Groups.All(g => Options.Any(o => o.Group == g && o.IsSelected)))
            Confirm();
    }

    public void Confirm()
    {
        var selected = Options.Where(o => o.IsSelected).ToList();
        _tcs.TrySetResult(new SpecSelectResult
        {
            SpecText = string.Join("/", selected.Select(o => o.Value)),
            PriceDelta = selected.Sum(o => o.PriceDelta),
        });
    }

    public void Cancel() => _tcs.TrySetResult(null);
}

public sealed class SpecSelectResult
{
    public string SpecText { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}

/// <summary>称重输入 ViewModel（kg，可接电子秤读数，也可手输）</summary>
public sealed class WeightInputViewModel : ViewModelBase
{
    private decimal _weightKg;
    private string? _error;

    public WeightInputViewModel(string productName, decimal unitPrice)
    {
        ProductName = productName;
        UnitPrice = unitPrice;
    }

    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public string UnitLabel => "kg";

    public decimal WeightKg
    {
        get => _weightKg;
        set
        {
            if (SetProperty(ref _weightKg, value))
            {
                RaisePropertyChanged(nameof(Amount));
                Error = null;
            }
        }
    }

    /// <summary>金额 = 重量 × 单价（算出来的，不是填的）</summary>
    public decimal Amount => Services.Ordering.OrderCalculator.CalcWeightedAmount(WeightKg, UnitPrice);

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>电子秤读数接入</summary>
    public void ApplyScaleWeight(decimal weightKg) => WeightKg = weightKg;

    public bool TryConfirm(out string? error)
    {
        error = null;
        if (WeightKg <= 0m)
        {
            error = "请输入有效重量";
            Error = error;
            return false;
        }
        return true;
    }
}

/// <summary>改价/折扣 ViewModel（仅店长，弹窗必须显示原值 + 二次确认）</summary>
public sealed class PriceEditViewModel : ViewModelBase
{
    private decimal _newPrice;
    private bool _confirmed;

    public PriceEditViewModel(string productName, decimal originalPrice)
    {
        ProductName = productName;
        OriginalPrice = originalPrice;
        _newPrice = originalPrice;
    }

    public string ProductName { get; }
    public decimal OriginalPrice { get; }

    public decimal NewPrice
    {
        get => _newPrice;
        set => SetProperty(ref _newPrice, value);
    }

    /// <summary>是否已二次确认</summary>
    public bool Confirmed
    {
        get => _confirmed;
        private set => SetProperty(ref _confirmed, value);
    }

    /// <summary>第一次点确认 → 要求二次确认；第二次才真正生效</summary>
    public bool RequestConfirm()
    {
        if (NewPrice < 0m) return false;
        if (!Confirmed)
        {
            Confirmed = true; // 显示"再次确认"态
            return false;
        }
        return true;
    }
}

/// <summary>挂单列表 ViewModel</summary>
public sealed class HangOrderViewModel : ViewModelBase
{
    public HangOrderViewModel(Services.Ordering.HangOrderStore store)
    {
        Store = store;
        Refresh();
    }

    public Services.Ordering.HangOrderStore Store { get; }
    public ObservableCollection<HangOrderSnapshotView> Hangs { get; } = new();

    public void Refresh()
    {
        Hangs.Clear();
        foreach (var h in Store.List()) Hangs.Add(new HangOrderSnapshotView(h));
    }
}

/// <summary>挂单行展示（桌号/人数/时间/金额/明细数）</summary>
public sealed class HangOrderSnapshotView : ViewModelBase
{
    private readonly Services.Ordering.HangOrderSnapshot _snapshot;

    public HangOrderSnapshotView(Services.Ordering.HangOrderSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public string HangId => _snapshot.HangId;
    public string DeskName => _snapshot.DeskName;
    public decimal Amount => _snapshot.Amount;
    public bool IsStale => _snapshot.IsStale;

    public string Summary =>
        $"{_snapshot.GuestCount}人 · {_snapshot.HangTime:HH:mm}挂单 · {_snapshot.Lines.Count}项明细";
}

/// <summary>备注 ViewModel（最多 30 字）</summary>
public sealed class RemarkViewModel : ViewModelBase
{
    private string _remark = string.Empty;

    public RemarkViewModel(string productName, string? initial = null)
    {
        ProductName = productName;
        _remark = initial ?? string.Empty;
    }

    public string ProductName { get; }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value?.Length > 30 ? value.Substring(0, 30) : (value ?? string.Empty));
    }

    public int RemainChars => 30 - Remark.Length;
}

/// <summary>退菜原因 ViewModel</summary>
public sealed class RefundReasonViewModel : ViewModelBase
{
    private string? _selectedReason;
    private string _customReason = string.Empty;

    public static readonly string[] PresetReasons = { "不要了", "上错了", "客人不要了", "品质问题" };

    /// <summary>预设原因（实例属性，供 XAML 绑定）</summary>
    public IReadOnlyList<string> Reasons => PresetReasons;

    public string? SelectedReason
    {
        get => _selectedReason;
        set => SetProperty(ref _selectedReason, value);
    }

    public string CustomReason
    {
        get => _customReason;
        set => SetProperty(ref _customReason, value);
    }

    public string? Reason => !string.IsNullOrWhiteSpace(CustomReason) ? CustomReason.Trim() : SelectedReason;
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
}
