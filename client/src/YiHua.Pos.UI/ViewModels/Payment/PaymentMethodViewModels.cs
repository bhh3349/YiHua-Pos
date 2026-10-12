using YiHua.Pos.UI.Services.Payment;

namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 现金支付 ViewModel：实收输入 + 找零计算 + 快捷金额。
/// </summary>
public class CashPaymentViewModel
{
    /// <summary>应收金额。</summary>
    public decimal PayableAmount { get; set; }

    /// <summary>实收金额（NumberPad 输入）。</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>找零（仅当实收>=应收时有效）。</summary>
    public decimal ChangeAmount => PaymentCalculator.IsSufficient(ReceivedAmount, PayableAmount)
        ? PaymentCalculator.CalculateChange(ReceivedAmount, PayableAmount)
        : 0m;

    /// <summary>实收是否不足。</summary>
    public bool IsInsufficient => !PaymentCalculator.IsSufficient(ReceivedAmount, PayableAmount);

    /// <summary>快捷金额选项。</summary>
    public List<decimal> QuickAmounts => PaymentCalculator.GetQuickAmounts(PayableAmount);

    /// <summary>选择快捷金额（可测试）。</summary>
    public void SelectQuickAmount(decimal amount) => ReceivedAmount = amount;

    /// <summary>校验（可测试）。</summary>
    public string? Validate()
    {
        if (IsInsufficient)
            return $"实收 ¥{ReceivedAmount:F2} 不足，应收 ¥{PayableAmount:F2}";
        return null;
    }
}

/// <summary>
/// 记账 ViewModel：客户选择/新建，额度校验。
/// </summary>
public class CreditPaymentViewModel
{
    /// <summary>应收金额。</summary>
    public decimal PayableAmount { get; set; }

    /// <summary>选择的客户。</summary>
    public CustomerDto? SelectedCustomer { get; set; }

    /// <summary>是否为店长（额度无上限）。</summary>
    public bool IsManager { get; set; }

    /// <summary>普通收银员记账额度上限（店长设置）。</summary>
    public decimal CreditLimit { get; set; } = 500m;

    /// <summary>
    /// 校验（可测试）：超额度拒绝（非店长）。
    /// </summary>
    public string? Validate()
    {
        if (SelectedCustomer == null)
            return "请选择记账客户";
        if (!IsManager && PayableAmount > CreditLimit)
            return $"超出记账额度（上限 ¥{CreditLimit:F2}），请联系店长";
        return null;
    }
}

/// <summary>客户 DTO。</summary>
public class CustomerDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Remark { get; set; }
    /// <summary>累计欠款。</summary>
    public decimal TotalDebt { get; set; }
    /// <summary>是否为临时客户（仅名字/电话）。</summary>
    public bool IsTemporary { get; set; }
}

/// <summary>
/// 自定义收款 ViewModel：记录金额/方式/凭据，不找零。
/// </summary>
public class CustomQrPaymentViewModel
{
    /// <summary>应收金额。</summary>
    public decimal PayableAmount { get; set; }

    /// <summary>实收金额（=应收，不找零）。</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>收款方式（微信/支付宝/银行卡/其他）。</summary>
    public string PayMethod { get; set; } = "微信";

    /// <summary>凭据号（选填）。</summary>
    public string? VoucherNo { get; set; }

    /// <summary>备注（选填）。</summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 确认收款（可测试）：实收必须等于应收（不找零）。
    /// </summary>
    public string? Validate()
    {
        if (ReceivedAmount != PayableAmount)
            return $"自定义收款不找零，实收应等于应收 ¥{PayableAmount:F2}";
        return null;
    }

    /// <summary>构建支付请求（可测试）。</summary>
    public PaymentRequest BuildRequest(string orderNo) => new()
    {
        OrderNo = orderNo,
        Amount = ReceivedAmount,
        Remark = Remark,
        Extra =
        {
            ["pay_method"] = PayMethod,
            ["voucher_no"] = VoucherNo ?? "",
            ["pay_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        },
    };
}
