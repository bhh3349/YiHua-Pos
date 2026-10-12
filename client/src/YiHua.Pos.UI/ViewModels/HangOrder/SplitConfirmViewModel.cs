using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>拆单确认 ViewModel（显示拆出/剩余金额二次确认）</summary>
public sealed class SplitConfirmViewModel : ViewModelBase
{
    private bool _isConfirmed;

    public decimal SplitAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int SplitItemCount { get; set; }

    public string SummaryText =>
        $"拆出 {SplitItemCount} 个菜，共 ¥{SplitAmount:F2}；剩余 ¥{RemainingAmount:F2}";

    public bool IsConfirmed
    {
        get => _isConfirmed;
        private set => SetProperty(ref _isConfirmed, value);
    }

    public void Confirm() => IsConfirmed = true;
    public void Cancel() => IsConfirmed = false;
}

/// <summary>批量清理 ViewModel（二次确认，显示总数和总金额）</summary>
public sealed class HangOrderCleanViewModel : ViewModelBase
{
    private readonly IHangOrderRepository _repo;
    private bool _isCleaning;
    private string? _errorMessage;

    public HangOrderCleanViewModel(IHangOrderRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public List<HangOrderRecord> ToClean { get; private set; } = new();

    public int CleanCount => ToClean.Count;
    public decimal CleanAmount => ToClean.Sum(r => r.Amount);

    public string ConfirmText =>
        $"确定清理 {CleanCount} 单，共 ¥{CleanAmount:F2} 吗？此操作不可恢复。";

    public bool IsCleaning
    {
        get => _isCleaning;
        private set => SetProperty(ref _isCleaning, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public async Task LoadTimedOutAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            var all = await _repo.GetAllAsync(ct).ConfigureAwait(false);
            ToClean = all.Where(r => HangOrderCalculator.IsTimedOut(r, timeout)).ToList();
            RaisePropertyChanged(nameof(ToClean));
            RaisePropertyChanged(nameof(CleanCount));
            RaisePropertyChanged(nameof(CleanAmount));
            RaisePropertyChanged(nameof(ConfirmText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
    }

    public async Task<int> CleanAsync(CancellationToken ct = default)
    {
        IsCleaning = true;
        ErrorMessage = null;
        try
        {
            int count = 0;
            foreach (var r in ToClean)
            {
                await _repo.RemoveAsync(r.Id, ct).ConfigureAwait(false);
                count++;
            }
            ToClean.Clear();
            RaisePropertyChanged(nameof(ToClean));
            RaisePropertyChanged(nameof(CleanCount));
            RaisePropertyChanged(nameof(CleanAmount));
            RaisePropertyChanged(nameof(ConfirmText));
            return count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"清理失败：{ex.Message}";
            return 0;
        }
        finally
        {
            IsCleaning = false;
        }
    }
}
