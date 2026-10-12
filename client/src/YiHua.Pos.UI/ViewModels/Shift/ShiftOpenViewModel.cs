using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

// 别名在 namespace 内部（CS0118 规避，见 2cab19f）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>开班 ViewModel：备用金必填（可预设），防重复开班</summary>
public class ShiftOpenViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;

    private decimal _openingFloat;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public ShiftOpenViewModel(IShiftService shiftService, decimal presetFloat = 500m)
    {
        _shiftService = shiftService;
        _openingFloat = presetFloat;
    }

    /// <summary>备用金（必填，可预设也可手输）</summary>
    public decimal OpeningFloat
    {
        get => _openingFloat;
        set => SetProperty(ref _openingFloat, value);
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

    public event EventHandler<ShiftEntity>? Opened;

    public async Task<bool> ConfirmAsync(long operatorId, string operatorName, string terminalNo,
        CancellationToken ct = default)
    {
        if (OpeningFloat < 0)
        {
            ErrorMessage = "备用金不能为负数";
            return false;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var shift = await _shiftService.OpenAsync(OpeningFloat, operatorId, operatorName, terminalNo, ct)
                .ConfigureAwait(false);
            Opened?.Invoke(this, shift);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            // 已有未关闭班：拒绝开新班
            ErrorMessage = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"开班失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
