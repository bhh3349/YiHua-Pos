using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.ViewModels.Desk;

/// <summary>
/// 桌台主视图 ViewModel：区域筛选 + 状态筛选 + 排序切换 + 30秒刷新占用时长。
/// </summary>
public class DeskMapViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;

    private string? _selectedArea;
    private DeskStatus? _statusFilter;
    private bool _idleFirst = true;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public DeskMapViewModel(IDeskRepository deskRepository)
    {
        _deskRepository = deskRepository;
        Desks = new ObservableCollection<DeskCardModel>();
        Areas = new ObservableCollection<string>();
    }

    public ObservableCollection<DeskCardModel> Desks { get; }
    public ObservableCollection<string> Areas { get; }

    /// <summary>选中的区域（null = 全部）</summary>
    public string? SelectedArea
    {
        get => _selectedArea;
        set { if (SetProperty(ref _selectedArea, value)) ApplyFilter(); }
    }

    /// <summary>状态筛选（null = 全部）</summary>
    public DeskStatus? StatusFilter
    {
        get => _statusFilter;
        set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); }
    }

    /// <summary>排序：true=空闲在前（默认，找空桌最快），false=占用在前</summary>
    public bool IdleFirst
    {
        get => _idleFirst;
        set { if (SetProperty(ref _idleFirst, value)) ApplyFilter(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsEmpty => Desks.Count == 0 && !IsLoading && string.IsNullOrEmpty(ErrorMessage);

    private List<DeskEntity> _allDesks = new();

    public async Task<DeskEntity?> GetDeskAsync(long id, CancellationToken ct = default)
        => await _deskRepository.GetByIdAsync(id, ct).ConfigureAwait(false);

    public async Task<DeskEntity?> GetFirstIdleDeskAsync(CancellationToken ct = default)
    {
        var all = await _deskRepository.GetDesksAsync(null, ct).ConfigureAwait(false);
        return all.FirstOrDefault(d => d.Status == DeskStatus.Idle);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var areas = await _deskRepository.GetAreasAsync(ct).ConfigureAwait(false);
            Areas.Clear();
            foreach (var a in areas.OrderBy(x => x.Sort))
                Areas.Add(a.Name);

            _allDesks = await _deskRepository.GetDesksAsync(null, ct).ConfigureAwait(false);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"桌台加载失败：{ex.Message}，请检查后重试";
        }
        finally
        {
            IsLoading = false;
            RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>刷新占用时长（每 30 秒由视图定时调用）</summary>
    public void RefreshElapsed()
    {
        var now = DateTime.Now;
        foreach (var d in Desks)
            d.ElapsedText = DeskCardModel.FormatElapsed(d.OpenTime, now);
    }

    private void ApplyFilter()
    {
        var query = _allDesks.AsEnumerable();
        if (!string.IsNullOrEmpty(SelectedArea))
            query = query.Where(d => d.AreaName == SelectedArea);
        if (StatusFilter != null)
            query = query.Where(d => d.Status == StatusFilter.Value);

        // 排序：空闲优先 vs 占用优先，同状态内按名称
        query = IdleFirst
            ? query.OrderBy(d => d.Status == DeskStatus.Idle ? 0 : 1).ThenBy(d => d.Name)
            : query.OrderBy(d => d.Status == DeskStatus.Occupied ? 0 : 1).ThenBy(d => d.Name);

        Desks.Clear();
        var now = DateTime.Now;
        foreach (var d in query)
        {
            Desks.Add(new DeskCardModel
            {
                Id = d.Id,
                Name = d.Name,
                AreaName = d.AreaName,
                Status = d.Status,
                GuestCount = d.GuestCount,
                CurrentAmount = d.CurrentAmount,
                OpenTime = d.OpenTime,
                ElapsedText = DeskCardModel.FormatElapsed(d.OpenTime, now),
            });
        }
        RaisePropertyChanged(nameof(IsEmpty));
    }
}
