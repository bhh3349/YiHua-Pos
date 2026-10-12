using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.Refund;

namespace YiHua.Pos.UI.ViewModels.Refund;

/// <summary>退菜原因（必填）</summary>
public enum RefundReason
{
    GuestCancelled,   // 客人不要了
    WrongDish,        // 上错菜
    QualityIssue,     // 菜品质量问题
    OutOfStock,       // 售罄/做不了
    Overcooked,       // 做坏了重做
    Complaint,        // 投诉补偿
    Other,            // 其他
}

public static class RefundReasonText
{
    public static string ToText(this RefundReason r) => r switch
    {
        RefundReason.GuestCancelled => "客人不要了",
        RefundReason.WrongDish => "上错菜",
        RefundReason.QualityIssue => "菜品质量问题",
        RefundReason.OutOfStock => "售罄/做不了",
        RefundReason.Overcooked => "做坏了重做",
        RefundReason.Complaint => "投诉补偿",
        _ => "其他",
    };
}

/// <summary>供 ListBox 显示的原因选项</summary>
public class RefundReasonOption
{
    public RefundReason Value { get; }
    public string Text { get; }
    public RefundReasonOption(RefundReason value) { Value = value; Text = value.ToText(); }
}

/// <summary>原因选择 ViewModel（退菜/退单/冲正共用，原因必填）</summary>
public class RefundReasonViewModel : ViewModelBase
{
    private RefundReason _reason = RefundReason.GuestCancelled;
    public RefundReason Reason
    {
        get => _reason;
        set { SetProperty(ref _reason, value); RaisePropertyChanged(nameof(IsValid)); }
    }

    private string _note = string.Empty;
    /// <summary>补充说明（选"其他"时必填）</summary>
    public string Note
    {
        get => _note;
        set { SetProperty(ref _note, value); RaisePropertyChanged(nameof(IsValid)); }
    }

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public bool IsValid =>
        Reason != RefundReason.Other || !string.IsNullOrWhiteSpace(Note);

    public string ReasonText =>
        Reason == RefundReason.Other && !string.IsNullOrWhiteSpace(Note)
            ? $"其他：{Note.Trim()}"
            : Reason.ToText();

    public bool Validate()
    {
        if (Reason == RefundReason.Other && string.IsNullOrWhiteSpace(Note))
        {
            Error = "选择「其他」时必须填写补充说明";
            return false;
        }
        Error = null;
        return true;
    }

    public static IReadOnlyList<RefundReason> AllReasons { get; } =
        Enum.GetValues<RefundReason>();

    public static IReadOnlyList<RefundReasonOption> ReasonOptions { get; } =
        AllReasons.Select(r => new RefundReasonOption(r)).ToList();
}
