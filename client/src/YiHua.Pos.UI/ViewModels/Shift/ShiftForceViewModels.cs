using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>强制交班 ViewModel：仅店长，二次确认，记录操作人并标注"强制"</summary>
public class ForceCloseViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;

    private decimal _actualCash;
    private DifferenceReason _reason = DifferenceReason.Miscounted;
    private bool _confirmed;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public ForceCloseViewModel(IShiftService shiftService, ShiftEntity shift)
    {
        _shiftService = shiftService;
        Shift = shift;
    }

    public ShiftEntity Shift { get; }

    public decimal ActualCash
    {
        get => _actualCash;
        set => SetProperty(ref _actualCash, value);
    }

    public DifferenceReason Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    /// <summary>二次确认勾选</summary>
    public bool Confirmed
    {
        get => _confirmed;
        set => SetProperty(ref _confirmed, value);
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public event EventHandler? ForceClosed;

    public async Task<bool> ConfirmAsync(string managerName, string managerRole, CancellationToken ct = default)
    {
        if (managerRole != "店长")
        {
            ErrorMessage = "强制交班仅店长可操作";
            return false;
        }
        if (!Confirmed)
        {
            ErrorMessage = "请勾选二次确认";
            return false;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            await _shiftService.ForceCloseAsync(Shift.Id, ActualCash, Reason, managerName, ct)
                .ConfigureAwait(false);
            ForceClosed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"强制交班失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>未交班拦截 ViewModel：阻塞级弹窗，不能一点就过</summary>
public class UnclosedShiftViewModel : ViewModelBase
{
    private bool _acknowledged;

    public UnclosedShiftViewModel(ShiftEntity shift)
    {
        Shift = shift;
    }

    public ShiftEntity Shift { get; }

    /// <summary>用户必须明确知晓（勾选"我已知晓"）才能继续</summary>
    public bool Acknowledged
    {
        get => _acknowledged;
        set => SetProperty(ref _acknowledged, value);
    }

    public string Message =>
        $"上一班（{Shift.OperatorName}，{Shift.StartAt:MM-dd HH:mm}开班）尚未交班，" +
        $"不能开新班。请先交班，或联系店长强制交班。";

    public bool CanProceed => Acknowledged;
}
