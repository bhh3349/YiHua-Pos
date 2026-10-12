using System.Collections.ObjectModel;
using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>交班历史行模型</summary>
public class ShiftHistoryRow
{
    public long Id { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal AmountTotal { get; set; }
    public decimal CashActual { get; set; }
    public decimal CashDiff { get; set; }
    public string DifferenceText => ShiftDifferencePolicy.FormatDifference(CashDiff);
    public DifferenceKind Kind => ShiftDifferencePolicy.Classify(CashDiff);
}

/// <summary>交班历史 ViewModel：可查、可重打、不可修改</summary>
public class ShiftHistoryViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;

    private DateTime _from = DateTime.Today.AddDays(-7);
    private DateTime _to = DateTime.Today;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public ShiftHistoryViewModel(IShiftService shiftService)
    {
        _shiftService = shiftService;
        Shifts = new ObservableCollection<ShiftHistoryRow>();
    }

    public ObservableCollection<ShiftHistoryRow> Shifts { get; }

    public DateTime From
    {
        get => _from;
        set { if (SetProperty(ref _from, value)) _ = LoadAsync(); }
    }

    public DateTime To
    {
        get => _to;
        set { if (SetProperty(ref _to, value)) _ = LoadAsync(); }
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsEmpty => Shifts.Count == 0 && !IsLoading && string.IsNullOrEmpty(ErrorMessage);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _shiftService.GetHistoryAsync(From.Date, To.Date.AddDays(1), ct).ConfigureAwait(false);
            Shifts.Clear();
            foreach (var s in list.OrderByDescending(x => x.StartAt))
            {
                Shifts.Add(new ShiftHistoryRow
                {
                    Id = s.Id,
                    StartAt = s.StartAt,
                    EndAt = s.EndAt,
                    OperatorName = s.OperatorName,
                    OrderCount = s.OrderCount,
                    AmountTotal = s.AmountTotal,
                    CashActual = s.CashActual,
                    CashDiff = s.CashDiff,
                });
            }
            RaisePropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"历史加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
