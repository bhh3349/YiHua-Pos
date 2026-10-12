using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using System.Collections.ObjectModel;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>挂单列表 ViewModel（取单入口）</summary>
public sealed class HangOrderViewModel : ViewModelBase
{
    private readonly IHangOrderRepository _repo;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    /// <summary>超时阈值（默认 4 小时，可配置）</summary>
    public TimeSpan TimeoutThreshold { get; set; } = TimeSpan.FromHours(4);

    public HangOrderViewModel(IHangOrderRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public ObservableCollection<HangOrderRecord> Records { get; } = new();

    /// <summary>过滤后的记录（搜索：桌号/客人数/挂单人）</summary>
    public IEnumerable<HangOrderRecord> FilteredRecords
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return Records;
            var kw = _searchText.Trim();
            return Records.Where(r =>
                r.DeskName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                r.OperatorName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                r.GuestCount.ToString() == kw);
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RaisePropertyChanged(nameof(FilteredRecords));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsEmpty => Records.Count == 0;

    public int TimedOutCount => Records.Count(r => HangOrderCalculator.IsTimedOut(r, TimeoutThreshold));

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _repo.GetAllAsync(ct: ct).ConfigureAwait(false);
            Records.Clear();
            foreach (var r in list) Records.Add(r);
            RaisePropertyChanged(nameof(FilteredRecords));
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(TimedOutCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载挂单失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        await _repo.RemoveAsync(id, ct).ConfigureAwait(false);
        var r = Records.FirstOrDefault(x => x.Id == id);
        if (r != null) Records.Remove(r);
        RaisePropertyChanged(nameof(FilteredRecords));
        RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(TimedOutCount));
    }

    public bool IsTimedOut(HangOrderRecord r) =>
        HangOrderCalculator.IsTimedOut(r, TimeoutThreshold);
}
