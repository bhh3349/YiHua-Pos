using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

/// <summary>差异原因 ViewModel（下拉选择）</summary>
public class ShiftDifferenceViewModel : ViewModelBase
{
    private DifferenceReason _reason = DifferenceReason.Miscounted;
    private string? _note;

    public decimal Difference { get; }

    public ShiftDifferenceViewModel(decimal difference)
    {
        Difference = difference;
    }

    public DifferenceReason Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    /// <summary>"其他"时的手输补充</summary>
    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string DifferenceText => ShiftDifferencePolicy.FormatDifference(Difference);
    public DifferenceKind Kind => ShiftDifferencePolicy.Classify(Difference);
    public bool NeedsNote => Reason == DifferenceReason.Other;
}
