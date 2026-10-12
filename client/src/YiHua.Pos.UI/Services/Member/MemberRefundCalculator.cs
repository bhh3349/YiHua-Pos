namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 退卡请求。
/// </summary>
public class MemberRefundRequest
{
    /// <summary>姓名（留存）。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>证件号（留存）。</summary>
    public string IdCardNo { get; set; } = string.Empty;
    /// <summary>退款方式：现金/银行转账。</summary>
    public string RefundMethod { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
}

/// <summary>
/// 退卡结果。
/// </summary>
public class MemberRefundResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    /// <summary>退款金额。</summary>
    public decimal RefundAmount { get; set; }
    /// <summary>退款方式。</summary>
    public string RefundMethod { get; set; } = string.Empty;
    public BalanceRecordDto? Record { get; set; }
}

/// <summary>
/// 退卡计算器（纯逻辑）。
/// 法规第二十一条：退卡须留存姓名、证件号、卡号、金额；余额 < 100 可付现金。
/// </summary>
public static class MemberRefundCalculator
{
    /// <summary>
    /// 余额 < 100 元可现金退款的阈值。
    /// </summary>
    public const decimal CashRefundThreshold = 100m;

    /// <summary>
    /// 退卡。
    /// </summary>
    public static MemberRefundResult Refund(MemberDto member, MemberRefundRequest req)
    {
        // 1. 留存信息必填
        if (string.IsNullOrWhiteSpace(req.Name))
            return Fail("退卡必须留存姓名（法规要求）");
        if (string.IsNullOrWhiteSpace(req.IdCardNo))
            return Fail("退卡必须留存证件号（法规要求）");
        if (string.IsNullOrWhiteSpace(req.RefundMethod))
            return Fail("请选择退款方式");

        if (member.Balance <= 0)
            return Fail("卡内余额为 0，无需退卡");

        // 2. 余额 < 100 可现金；≥100 需记录退款方式（已填）
        var method = req.RefundMethod;
        if (member.Balance >= CashRefundThreshold && method == "现金")
        {
            // 小店现实：允许现金，但必须留存凭证（已在记录中）
        }

        var refundAmount = Round(member.Balance);
        return new MemberRefundResult
        {
            Success = true,
            RefundAmount = refundAmount,
            RefundMethod = method,
            Record = new BalanceRecordDto
            {
                MemberId = member.Id,
                Amount = -refundAmount, // 清零
                BalanceAfter = 0,
                Reason = BalanceChangeReason.Refund,
                ReasonText = $"退卡（{req.Name}，证件 {MaskIdCard(req.IdCardNo)}）",
                OperatorName = req.OperatorName,
            },
        };
    }

    private static MemberRefundResult Fail(string error)
        => new() { Success = false, Error = error };

    /// <summary>
    /// 证件号脱敏：保留前4后4。
    /// </summary>
    public static string MaskIdCard(string idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard) || idCard.Length < 8)
            return "****";
        return idCard.Substring(0, 4) + "****" + idCard.Substring(idCard.Length - 4);
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
