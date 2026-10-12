using YiHua.Pos.UI.Services.Payment;

namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 结账主 ViewModel：订单明细（只读）+ 应收 + 折扣 + 支付方式选择。
/// </summary>
public class PaymentViewModel
{
    public PaymentViewModel()
    {
        SubmitMachine = new PaymentSubmitStateMachine();
    }

    /// <summary>订单号。</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>桌台信息（如"A08 · 4人"）。</summary>
    public string DeskInfo { get; set; } = string.Empty;

    /// <summary>订单明细（只读，核对用）。</summary>
    public List<PaymentOrderItemDto> Items { get; } = new();

    /// <summary>商品总额。</summary>
    public decimal TotalAmount => Items.Sum(i => i.Amount);

    /// <summary>折扣额。</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>折扣说明（如"9折"/"抹零"）。</summary>
    public string? DiscountLabel { get; private set; }

    /// <summary>应收金额。</summary>
    public decimal PayableAmount => TotalAmount - DiscountAmount;

    /// <summary>选择的支付方式。</summary>
    public PaymentMethodKind SelectedMethod { get; set; } = PaymentMethodKind.Cash;

    public PaymentSubmitStateMachine SubmitMachine { get; }

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 应用折扣（仅店长，可测试）。
    /// </summary>
    public void ApplyDiscount(DiscountRequest request)
    {
        if (!request.IsManager)
            throw new UnauthorizedAccessException("折扣/抹零仅店长可操作");

        var (payable, discount) = request.Type switch
        {
            DiscountType.Rate => PaymentCalculator.ApplyRateDiscount(TotalAmount, request.Value),
            DiscountType.Fixed => PaymentCalculator.ApplyFixedDiscount(TotalAmount, request.Value),
            DiscountType.RoundToJiao => PaymentCalculator.ApplyRoundingDiscount(TotalAmount, false) switch { var r => (r.Payable, r.Discount) },
            DiscountType.RoundToYuan => PaymentCalculator.ApplyRoundingDiscount(TotalAmount, true) switch { var r => (r.Payable, r.Discount) },
            _ => throw new ArgumentOutOfRangeException(),
        };

        DiscountAmount = discount;
        DiscountLabel = request.Type switch
        {
            DiscountType.Rate => $"{request.Value * 10:F1}折",
            DiscountType.Fixed => $"减¥{request.Value:F2}",
            DiscountType.RoundToJiao => "抹零到角",
            DiscountType.RoundToYuan => "抹零到元",
            _ => null,
        };
    }

    /// <summary>清除折扣。</summary>
    public void ClearDiscount()
    {
        DiscountAmount = 0;
        DiscountLabel = null;
    }
}

/// <summary>结账订单明细 DTO。</summary>
public class PaymentOrderItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public string SpecText { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public bool IsGift { get; set; }
}

/// <summary>支付方式。</summary>
public enum PaymentMethodKind
{
    Cash,
    Credit,
    CustomQr,
    Wechat,
    Alipay,
}
