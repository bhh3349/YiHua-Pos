using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.GroupBuy;

namespace YiHua.Pos.UI.ViewModels.GroupBuy;

/// <summary>验券状态</summary>
public enum VerifyState
{
    Idle,       // 等待扫码
    Loading,    // 验券中
    Success,    // 验券成功
    Error,      // 验券失败
    Offline,    // 断网
}

/// <summary>
/// 验券 ViewModel（扫码优先）。
/// 扫码枪表现为「键盘输入 + 回车」，输入框 autofocus，回车即触发。
/// </summary>
public class CouponVerifyViewModel : ViewModelBase
{
    private readonly IGroupBuyClient _client;

    public Order Order { get; }
    public long StoreId { get; }
    public string OperatorName { get; }

    private string _inputCode = string.Empty;
    /// <summary>扫码/手工输入框（autofocus）</summary>
    public string InputCode
    {
        get => _inputCode;
        set => SetProperty(ref _inputCode, value);
    }

    private VerifyState _state = VerifyState.Idle;
    public VerifyState State
    {
        get => _state;
        private set
        {
            SetProperty(ref _state, value);
            RaisePropertyChanged(nameof(IsIdle));
            RaisePropertyChanged(nameof(IsLoading));
            RaisePropertyChanged(nameof(IsSuccess));
            RaisePropertyChanged(nameof(IsError));
            RaisePropertyChanged(nameof(IsOffline));
        }
    }

    public bool IsIdle => State == VerifyState.Idle;
    public bool IsLoading => State == VerifyState.Loading;
    public bool IsSuccess => State == VerifyState.Success;
    public bool IsError => State == VerifyState.Error;
    public bool IsOffline => State == VerifyState.Offline;

    private CouponInfo? _coupon;
    public CouponInfo? Coupon
    {
        get => _coupon;
        private set
        {
            SetProperty(ref _coupon, value);
            RaisePropertyChanged(nameof(CouponDisplay));
        }
    }

    /// <summary>券面大字显示</summary>
    public string CouponDisplay => Coupon == null ? string.Empty :
        $"{Coupon.PlatformName} · {Coupon.CouponName}\n" +
        $"抵扣 ¥{Coupon.DiscountAmount:F2}（面值 ¥{Coupon.FaceValue:F2}）\n" +
        $"有效期 {Coupon.ValidFrom} 至 {Coupon.ValidTo}";

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private string? _offlineHint;
    /// <summary>断网提示：说清该做什么，不卡收银</summary>
    public string? OfflineHint
    {
        get => _offlineHint;
        private set => SetProperty(ref _offlineHint, value);
    }

    private bool _redeeming;
    /// <summary>核销中：按钮点击后立即禁用，防重复点击</summary>
    public bool Redeeming
    {
        get => _redeeming;
        private set => SetProperty(ref _redeeming, value);
    }

    private bool _redeemed;
    public bool Redeemed
    {
        get => _redeemed;
        private set => SetProperty(ref _redeemed, value);
    }

    /// <summary>抵扣前应收</summary>
    public decimal PayableBefore => Order.AmountPay;

    /// <summary>抵扣后应收</summary>
    public decimal PayableAfter => Coupon == null ? PayableBefore :
        Math.Round(PayableBefore - Coupon.DiscountAmount, 2, MidpointRounding.AwayFromZero);

    public CouponVerifyViewModel(Order order, long storeId, string operatorName, IGroupBuyClient? client = null)
    {
        Order = order;
        StoreId = storeId;
        OperatorName = operatorName;
        _client = client ?? new FakeGroupBuyClient();
    }

    /// <summary>
    /// 扫码枪回车 / 手工输入确认 → 验券。
    /// 扫码枪前缀后缀过滤 + 券码归一化。
    /// </summary>
    public async Task VerifyAsync(string? scannerPrefix = null, string? scannerSuffix = null, CancellationToken ct = default)
    {
        string code = CouponInputNormalizer.Process(InputCode, scannerPrefix, scannerSuffix);
        if (string.IsNullOrEmpty(code))
        {
            State = VerifyState.Error;
            ErrorMessage = "券码为空，请扫码或手工输入";
            return;
        }

        State = VerifyState.Loading;
        ErrorMessage = null;
        OfflineHint = null;

        // 默认平台：从券码前缀判断，或让收银员选择；此处简化用美团
        var result = await _client.VerifyAsync("meituan", code, StoreId, ct).ConfigureAwait(false);

        if (result.ErrorCode == "NETWORK_ERROR")
        {
            State = VerifyState.Offline;
            OfflineHint = "券验证需要联网。可以按券面金额直接打折。";
            // 券信息照样记（平台 + 券码），月底对账用
            Order.GroupBuyPlatform = PlatformNames.ToChinese("meituan");
            Order.GroupBuyCouponCode = code;
            return;
        }

        if (!result.Valid || result.Coupon == null)
        {
            State = VerifyState.Error;
            ErrorMessage = CouponErrorMapper.Map(result.ErrorCode, result.Message);
            return;
        }

        // 抵扣额不能超过应收
        var (ok, err) = GroupBuyCalculator.ValidateDiscount(result.Coupon.DiscountAmount, PayableBefore);
        if (!ok)
        {
            State = VerifyState.Error;
            ErrorMessage = err;
            return;
        }

        Coupon = result.Coupon;
        State = VerifyState.Success;
        RaisePropertyChanged(nameof(PayableAfter));
    }

    /// <summary>
    /// 确认核销：幂等，按钮点击后立即禁用。
    /// 成功后写回 Order.GroupBuyPlatform / GroupBuyCouponCode / AmountGroupBuy。
    /// 核销失败不改订单。
    /// </summary>
    public async Task<bool> RedeemAsync(CancellationToken ct = default)
    {
        if (Coupon == null || Redeeming || Redeemed)
            return false;

        Redeeming = true;
        try
        {
            var result = await _client.RedeemAsync(
                Coupon.PlatformCode, Coupon.CouponCode, StoreId, Order.Id, ct).ConfigureAwait(false);
            if (!result.Success)
            {
                State = VerifyState.Error;
                ErrorMessage = CouponErrorMapper.Map(result.ErrorCode, result.Message);
                return false;
            }

            // 写回订单团购字段（抵扣进优惠口径，由 GroupBuyCalculator 接入 Ordering）
            Order.GroupBuyPlatform = Coupon.PlatformName;
            Order.GroupBuyCouponCode = Coupon.CouponCode;
            Order.AmountGroupBuy = GroupBuyCalculator.PickDiscount(Coupon);

            Redeemed = true;
            return true;
        }
        finally
        {
            Redeeming = false;
        }
    }

    /// <summary>重新扫码</summary>
    public void Reset()
    {
        InputCode = string.Empty;
        Coupon = null;
        ErrorMessage = null;
        OfflineHint = null;
        Redeemed = false;
        State = VerifyState.Idle;
    }
}
