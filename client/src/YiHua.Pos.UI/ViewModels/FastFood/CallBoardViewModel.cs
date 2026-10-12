using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>
/// 叫号屏 ViewModel。
/// 数据推送到 ICounterDisplay（副屏）；副屏不可用时降级标记由 UI 层处理。
/// </summary>
public sealed class CallBoardViewModel : ViewModelBase
{
    private readonly ICounterDisplay _display;
    private string _currentNumber = string.Empty;
    private string _currentStatus = string.Empty;
    private bool _showAmount;
    private decimal _currentAmount;
    private bool _isDowngraded;

    public CallBoardViewModel(ICounterDisplay display)
    {
        _display = display ?? throw new ArgumentNullException(nameof(display));
        _isDowngraded = !display.IsAvailable;
    }

    /// <summary>当前叫号</summary>
    public string CurrentNumber
    {
        get => _currentNumber;
        private set => SetProperty(ref _currentNumber, value);
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        private set => SetProperty(ref _currentStatus, value);
    }

    /// <summary>是否显示金额</summary>
    public bool ShowAmount
    {
        get => _showAmount;
        set { if (SetProperty(ref _showAmount, value)) Push(); }
    }

    public decimal CurrentAmount
    {
        get => _currentAmount;
        private set => SetProperty(ref _currentAmount, value);
    }

    /// <summary>等待中的号（主屏降级显示用）</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> WaitingNumbers { get; } = new();

    /// <summary>是否已降级到主屏（副屏不可用）</summary>
    public bool IsDowngraded
    {
        get => _isDowngraded;
        private set => SetProperty(ref _isDowngraded, value);
    }

    public string DowngradeHint => IsDowngraded ? "副屏未连接，已降级到主屏显示" : string.Empty;

    /// <summary>更新叫号数据并推送</summary>
    public void Update(string currentNumber, string status, IEnumerable<string> waiting, int waitingCount, decimal amount = 0m)
    {
        CurrentNumber = currentNumber;
        CurrentStatus = status;
        CurrentAmount = amount;

        WaitingNumbers.Clear();
        foreach (var w in waiting.Take(8)) WaitingNumbers.Add(w);

        Push();
        RaisePropertyChanged(nameof(DowngradeHint));
    }

    private void Push()
    {
        IsDowngraded = !_display.IsAvailable;
        if (_display.IsAvailable)
        {
            _display.Show(new CallBoardData
            {
                CurrentNumber = CurrentNumber,
                CurrentStatus = CurrentStatus,
                WaitingNumbers = WaitingNumbers.ToList(),
                WaitingCount = WaitingNumbers.Count,
                ShowAmount = ShowAmount,
                CurrentAmount = CurrentAmount,
            });
        }
        // 降级时不推送，UI 层直接用本 ViewModel 的属性渲染主屏叫号视图
    }
}
