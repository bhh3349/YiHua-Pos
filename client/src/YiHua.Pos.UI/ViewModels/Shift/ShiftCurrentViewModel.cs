using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.ViewModels.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>班中实时统计 ViewModel</summary>
public class ShiftCurrentViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IOrderRepository _orderRepository;

    private ShiftEntity? _currentShift;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    // 统计字段
    private int _orderCount;
    private decimal _cashAmount;
    private decimal _customAmount;
    private decimal _totalDiscount;
    private int _refundCount;
    private decimal _refundAmount;
    private int _reversalCount;
    private decimal _reversalAmount;
    private decimal _expectedCash;

    public ShiftCurrentViewModel(IShiftService shiftService, IOrderRepository orderRepository)
    {
        _shiftService = shiftService;
        _orderRepository = orderRepository;
    }

    public ShiftEntity? CurrentShift
    {
        get => _currentShift;
        private set => SetProperty(ref _currentShift, value);
    }

    public int OrderCount { get => _orderCount; private set => SetProperty(ref _orderCount, value); }
    public decimal CashAmount { get => _cashAmount; private set => SetProperty(ref _cashAmount, value); }
    public decimal CustomAmount { get => _customAmount; private set => SetProperty(ref _customAmount, value); }
    public decimal TotalDiscount { get => _totalDiscount; private set => SetProperty(ref _totalDiscount, value); }
    public int RefundCount { get => _refundCount; private set => SetProperty(ref _refundCount, value); }
    public decimal RefundAmount { get => _refundAmount; private set => SetProperty(ref _refundAmount, value); }
    public int ReversalCount { get => _reversalCount; private set => SetProperty(ref _reversalCount, value); }
    public decimal ReversalAmount { get => _reversalAmount; private set => SetProperty(ref _reversalAmount, value); }

    /// <summary>钱箱应有 = 备用金 + 现金收入 - 现金退款</summary>
    public decimal ExpectedCash { get => _expectedCash; private set => SetProperty(ref _expectedCash, value); }

    /// <summary>本班应收合计</summary>
    public decimal TotalAmount => CashAmount + CustomAmount;

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool HasShift => CurrentShift != null;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            CurrentShift = await _shiftService.GetCurrentAsync(ct).ConfigureAwait(false);
            if (CurrentShift == null)
                return;

            var orders = await _orderRepository.GetByShiftIdAsync(CurrentShift.Id, ct).ConfigureAwait(false);

            OrderCount = ShiftCalculator.OrderCount(orders);
            CashAmount = ShiftCalculator.SumByPayType(orders, PayType.Cash);
            CustomAmount = ShiftCalculator.SumByPayType(orders, PayType.Custom);
            TotalDiscount = ShiftCalculator.TotalDiscount(orders);

            var (rc, ra) = ShiftCalculator.RefundStats(orders);
            RefundCount = rc;
            RefundAmount = ra;

            // 冲正单：负数单
            var reversals = orders.Where(o => o.AmountPay < 0).ToList();
            ReversalCount = reversals.Count;
            ReversalAmount = reversals.Sum(o => o.AmountPay);

            var cashIncome = ShiftCalculator.CashIncome(orders);
            var cashRefund = ShiftCalculator.CashRefund(orders);
            ExpectedCash = ShiftCalculator.ExpectedCashInDrawer(CurrentShift.CashOpen, cashIncome, cashRefund);

            RaisePropertyChanged(nameof(TotalAmount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"统计加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
