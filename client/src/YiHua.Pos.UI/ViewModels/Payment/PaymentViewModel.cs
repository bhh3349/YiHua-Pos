using YiHua.Pos.UI.Services.Payment;

namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 结账主 ViewModel：订单明细（只读）+ 应收 + 整单优惠 + 支付方式 + 实收找零。
///
/// 优惠口径（与参考端 t_order_discount.activityId 对齐）：
///   应收 = 原价 - 总优惠
///   总优惠 = 整单优惠（抹零/打折/减免/改价/免单）+ 行优惠（已在明细里扣掉）
/// 抹零与打折**可以叠加**，但每次只保留最后一次整单优惠 ——
/// 收银员改主意时应该重选，而不是把两种优惠叠起来算错账。
/// </summary>
public class PaymentViewModel : ViewModelBase
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

    /// <summary>商品总额（原价）。</summary>
    public decimal TotalAmount => Items.Sum(i => i.Amount);

    /// <summary>整单优惠额（抹零/打折/减免/改价/免单）。</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>优惠说明（如"9折"、"抹零到角"）。</summary>
    public string? DiscountLabel { get; private set; }

    /// <summary>优惠类型（报表按 activityId 分类）。</summary>
    public DiscountType? AppliedDiscountType { get; private set; }

    /// <summary>应收金额。</summary>
    public decimal PayableAmount => PaymentCalculator.Round(
        Math.Max(0m, TotalAmount - DiscountAmount));

    /// <summary>选择的支付方式。</summary>
    public PaymentMethodKind SelectedMethod { get; set; } = PaymentMethodKind.Cash;

    /// <summary>实收金额（现金时由收银员输入；其他方式等于应收）。</summary>
    public decimal ReceivedAmount { get; private set; }

    /// <summary>找零。</summary>
    public decimal ChangeAmount => PaymentCalculator.IsSufficient(ReceivedAmount, PayableAmount)
        ? PaymentCalculator.CalculateChange(ReceivedAmount, PayableAmount)
        : 0m;

    /// <summary>实收是否不足（现金支付时用来禁用结账按钮）。</summary>
    public bool IsInsufficient => SelectedMethod == PaymentMethodKind.Cash
        && ReceivedAmount > 0
        && ReceivedAmount < PayableAmount;

    /// <summary>快捷金额选项（现金常用面额）。</summary>
    public List<decimal> QuickAmounts => PaymentCalculator.GetQuickAmounts(PayableAmount);

    public PaymentSubmitStateMachine SubmitMachine { get; }

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 应用整单优惠（仅店长，可测试）。
    /// 每次调用都会**替换**上一次的整单优惠，不会叠加 —— 改主意要重选。
    /// </summary>
    public void ApplyDiscount(DiscountRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.IsManager)
            throw new UnauthorizedAccessException("折扣/抹零/免单仅店长可操作");

        var total = TotalAmount;

        var (payable, discount) = request.Type switch
        {
            DiscountType.Rate => PaymentCalculator.ApplyRateDiscount(total, request.Value),
            DiscountType.Fixed => PaymentCalculator.ApplyFixedDiscount(total, request.Value),
            DiscountType.RoundToJiao => PaymentCalculator.ApplyRoundingDiscount(total, false)
                is var r ? (r.Payable, r.Discount) : (total, 0m),
            DiscountType.RoundToYuan => PaymentCalculator.ApplyRoundingDiscount(total, true)
                is var r ? (r.Payable, r.Discount) : (total, 0m),
            // 整单改价：直接把应收改成指定金额（不能改成负数）
            DiscountType.Override => (Math.Max(0m, request.Value), Math.Max(0m, total - Math.Max(0m, request.Value))),
            // 免单：整单 100% 优惠
            DiscountType.Free => (0m, total),
            _ => throw new ArgumentOutOfRangeException(nameof(request),
                $"整单优惠不支持类型 {request.Type}（行级优惠请在明细里做）"),
        };

        DiscountAmount = PaymentCalculator.Round(discount);
        AppliedDiscountType = request.Type;
        DiscountLabel = BuildLabel(request, discount);

        // 优惠变了，实收要跟着重算，否则找零还是按老应收算的
        if (SelectedMethod == PaymentMethodKind.Cash && ReceivedAmount > 0)
            SetReceivedAmount(ReceivedAmount);

        RaisePropertyChanged(nameof(DiscountAmount));
        RaisePropertyChanged(nameof(DiscountLabel));
        RaisePropertyChanged(nameof(PayableAmount));
        RaisePropertyChanged(nameof(ChangeAmount));
        RaisePropertyChanged(nameof(IsInsufficient));
        RaisePropertyChanged(nameof(QuickAmounts));
    }

    /// <summary>清除整单优惠。</summary>
    public void ClearDiscount()
    {
        DiscountAmount = 0;
        DiscountLabel = null;
        AppliedDiscountType = null;
        RaisePropertyChanged(nameof(DiscountAmount));
        RaisePropertyChanged(nameof(DiscountLabel));
        RaisePropertyChanged(nameof(PayableAmount));
        RaisePropertyChanged(nameof(ChangeAmount));
        RaisePropertyChanged(nameof(IsInsufficient));
        RaisePropertyChanged(nameof(QuickAmounts));
    }

    /// <summary>选择支付方式。非现金方式实收自动等于应收（不找零）。</summary>
    public void SelectMethod(PaymentMethodKind method)
    {
        SelectedMethod = method;
        ReceivedAmount = method == PaymentMethodKind.Cash ? 0m : PayableAmount;

        RaisePropertyChanged(nameof(SelectedMethod));
        RaisePropertyChanged(nameof(ReceivedAmount));
        RaisePropertyChanged(nameof(ChangeAmount));
        RaisePropertyChanged(nameof(IsInsufficient));
        RaisePropertyChanged(nameof(QuickAmounts));
    }

    /// <summary>输入实收金额（现金）。</summary>
    public void SetReceivedAmount(decimal amount)
    {
        ReceivedAmount = amount < 0 ? 0m : PaymentCalculator.Round(amount);
        RaisePropertyChanged(nameof(ReceivedAmount));
        RaisePropertyChanged(nameof(ChangeAmount));
        RaisePropertyChanged(nameof(IsInsufficient));
    }

    /// <summary>收银员按「正好」：实收 = 应收。</summary>
    public void SetExactAmount() => SetReceivedAmount(PayableAmount);

    /// <summary>
    /// 结账前校验。返回 null 表示可以提交，否则是给收银员看的说明。
    /// </summary>
    public string? Validate()
    {
        if (Items.Count == 0)
            return "订单没有明细，不能结账";

        if (SelectedMethod == PaymentMethodKind.Cash && ReceivedAmount < PayableAmount)
            return $"实收 ¥{ReceivedAmount:F2} 不足应收 ¥{PayableAmount:F2}";

        if (SelectedMethod != PaymentMethodKind.Cash && ReceivedAmount != PayableAmount)
            return $"该收款方式不找零，实收应等于应收 ¥{PayableAmount:F2}";

        return null;
    }

    /// <summary>结账成功后重置，准备下一单。</summary>
    public void ResetForNextOrder()
    {
        Items.Clear();
        OrderNo = string.Empty;
        DeskInfo = string.Empty;
        ClearDiscount();
        ReceivedAmount = 0m;
        SelectedMethod = PaymentMethodKind.Cash;
        SubmitMachine.Reset();
        RaisePropertyChanged(nameof(TotalAmount));
        RaisePropertyChanged(nameof(PayableAmount));
        RaisePropertyChanged(nameof(ReceivedAmount));
    }

    private static string BuildLabel(DiscountRequest request, decimal discount) => request.Type switch
    {
        DiscountType.Rate => $"{request.Value * 10:F1}折",
        DiscountType.Fixed => $"减 ¥{request.Value:F2}",
        DiscountType.RoundToJiao => "抹分",
        DiscountType.RoundToYuan => "抹角",
        DiscountType.Override => $"改价 ¥{request.Value:F2}",
        DiscountType.Free => "免单",
        _ => $"优惠 ¥{discount:F2}",
    };
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

/// <summary>
/// 支付方式。
/// 编号与参考端 cy_pay_mode 的实际用法对齐；QrCode/Bank 是预留。
/// </summary>
public enum PaymentMethodKind
{
    /// <summary>现金：收钱、找零。</summary>
    Cash = 1,

    /// <summary>自定义收款（商家自己的收款码，系统只记账，不处理资金）。</summary>
    CustomQr = 2,

    /// <summary>挂账（记在客户名下，以后还）。</summary>
    Credit = 3,

    /// <summary>会员卡余额（二期做储值，一期先留入口）。</summary>
    MemberCard = 4,

    /// <summary>团购券核销（美团/抖音券，扫客人手机上的码）。</summary>
    GroupBuy = 5,

    // ---- 以下为预留，一期不实现 ----
    QrCode = 90,
    Bank = 91,
}