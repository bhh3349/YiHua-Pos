using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>
/// 交班对账 ViewModel（核心）：左右分栏（系统数据 vs 实点数据），
/// 差额最醒目，短款警告色/溢余中性色，原因必填。
/// </summary>
public class ShiftCloseViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IOrderRepository _orderRepository;
    private readonly ShiftDifferencePolicy _policy = new();

    private ShiftEntity? _shift;
    private decimal _actualCash;
    private DifferenceReason _reason = DifferenceReason.Miscounted;
    private string? _reasonNote;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    // 系统数据
    private decimal _cashShould;
    private decimal _creditAmount;
    private decimal _customAmount;

    public ShiftCloseViewModel(IShiftService shiftService, IOrderRepository orderRepository)
    {
        _shiftService = shiftService;
        _orderRepository = orderRepository;
    }

    public ShiftEntity? Shift
    {
        get => _shift;
        private set => SetProperty(ref _shift, value);
    }

    /// <summary>系统：现金应收</summary>
    public decimal CashShould { get => _cashShould; private set => SetProperty(ref _cashShould, value); }

    /// <summary>系统：记账</summary>
    public decimal CreditAmount { get => _creditAmount; private set => SetProperty(ref _creditAmount, value); }

    /// <summary>系统：自定义收款</summary>
    public decimal CustomAmount { get => _customAmount; private set => SetProperty(ref _customAmount, value); }

    /// <summary>系统合计</summary>
    public decimal SystemTotal => CashShould + CreditAmount + CustomAmount;

    /// <summary>实点：钱箱实点金额（人工输入）</summary>
    public decimal ActualCash
    {
        get => _actualCash;
        set
        {
            if (SetProperty(ref _actualCash, value))
            {
                RaisePropertyChanged(nameof(Difference));
                RaisePropertyChanged(nameof(DifferenceKind));
                RaisePropertyChanged(nameof(DifferenceText));
                RaisePropertyChanged(nameof(RequiresManager));
            }
        }
    }

    /// <summary>实点：备用金（下一班留多少，老板决定；默认=本班备用金）</summary>
    public decimal OpeningFloatForNext { get; set; }

    /// <summary>差额 = 实点 - 应有</summary>
    public decimal Difference => ShiftCalculator.CalculateDifference(ActualCash, ExpectedCash);

    public DifferenceKind DifferenceKind => ShiftDifferencePolicy.Classify(Difference);

    /// <summary>差额显示：带正负号和绝对值</summary>
    public string DifferenceText => ShiftDifferencePolicy.FormatDifference(Difference);

    /// <summary>是否短款超阈值（需店长确认）</summary>
    public bool RequiresManager => _policy.RequiresManager(Difference);

    /// <summary>钱箱应有（系统计算）</summary>
    public decimal ExpectedCash { get; private set; }

    public DifferenceReason Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string? ReasonNote
    {
        get => _reasonNote;
        set => SetProperty(ref _reasonNote, value);
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    /// <summary>确认弹窗显示的具体影响</summary>
    public string ImpactText =>
        $"交班后本班（{Shift?.OperatorName}）将关闭，差额 {DifferenceText}，" +
        $"下一班备用金 ¥{OpeningFloatForNext:F2}。已关闭的班不允许修改。";

    public event EventHandler? Closed;

    public async Task LoadAsync(long shiftId, CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var shifts = await _shiftService.GetHistoryAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), ct)
                .ConfigureAwait(false);
            Shift = shifts.FirstOrDefault(s => s.Id == shiftId)
                ?? await _shiftService.GetCurrentAsync(ct).ConfigureAwait(false);
            if (Shift == null)
            {
                ErrorMessage = "没有进行中的班次";
                return;
            }

            var orders = await _orderRepository.GetByShiftIdAsync(Shift.Id, ct).ConfigureAwait(false);
            CashShould = ShiftCalculator.SumByPayType(orders, PayType.Cash);
            CustomAmount = ShiftCalculator.SumByPayType(orders, PayType.Custom);
            // 记账：暂按 Custom 归类，待 Domain 增加记账类型（见缺口清单）
            CreditAmount = 0;

            var cashIncome = ShiftCalculator.CashIncome(orders);
            var cashRefund = ShiftCalculator.CashRefund(orders);
            ExpectedCash = ShiftCalculator.ExpectedCashInDrawer(Shift.CashOpen, cashIncome, cashRefund);
            Shift.CashShould = ExpectedCash; // 存档系统计算值

            OpeningFloatForNext = Shift.CashOpen; // 默认沿用

            RaisePropertyChanged(nameof(SystemTotal));
            RaisePropertyChanged(nameof(Difference));
            RaisePropertyChanged(nameof(DifferenceText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"对账数据加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> ConfirmAsync(string operatorName, CancellationToken ct = default)
    {
        if (Shift == null)
        {
            ErrorMessage = "没有可交的班次";
            return false;
        }
        // 差额原因必填（持平也需要确认，可选"无差异"——此处要求明确选择）
        // 注：DifferenceReason 枚举必有值，UI 层确保用户已选择

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            await _shiftService.CloseAsync(Shift.Id, ActualCash, Reason, ReasonNote, operatorName, ct)
                .ConfigureAwait(false);
            Closed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"交班失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
