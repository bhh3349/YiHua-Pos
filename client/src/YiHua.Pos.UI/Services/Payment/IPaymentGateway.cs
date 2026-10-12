namespace YiHua.Pos.UI.Services.Payment;

/// <summary>
/// 支付网关接口（预留线上支付）。
/// 现在只有 CustomQrPaymentGateway 实现；微信/支付宝仅骨架。
/// </summary>
public interface IPaymentGateway
{
    /// <summary>网关名称（如"自定义收款"/"微信支付"）。</summary>
    string Name { get; }

    /// <summary>是否可用（未配置商户号时 false）。</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 处理支付。
    /// </summary>
    /// <param name="request">支付请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>支付结果</returns>
    Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default);
}

/// <summary>
/// 支付请求。
/// </summary>
public class PaymentRequest
{
    public string OrderNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Remark { get; set; }
    public Dictionary<string, string> Extra { get; } = new();
}

/// <summary>
/// 支付结果。
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? Message { get; set; }
    public bool IsNotEnabled => !Success && Message == "暂未启用";

    public static PaymentResult Ok(string? transactionId = null)
        => new() { Success = true, TransactionId = transactionId };

    public static PaymentResult Fail(string message)
        => new() { Success = false, Message = message };

    public static PaymentResult NotEnabled()
        => new() { Success = false, Message = "暂未启用" };
}
