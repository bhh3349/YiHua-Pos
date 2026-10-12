namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>店长授权 ViewModel：整单退单超阈值时需要</summary>
public class RefundAuthViewModel : ViewModelBase
{
    public decimal Amount { get; }
    public decimal Threshold { get; }
    public string OperatorName { get; }

    private string _managerName = string.Empty;
    /// <summary>授权店长姓名（必填，记录用）</summary>
    public string ManagerName
    {
        get => _managerName;
        set => SetProperty(ref _managerName, value);
    }

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public string PromptText =>
        $"退款 ¥{Amount:F2} 超过 ¥{Threshold:F2}，需要店长授权";

    public RefundAuthViewModel(decimal amount, decimal threshold, string operatorName)
    {
        Amount = amount;
        Threshold = threshold;
        OperatorName = operatorName;
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ManagerName))
        {
            Error = "请输入授权店长姓名";
            return false;
        }
        Error = null;
        return true;
    }
}
