namespace YiHua.Pos.UI.Services.Payment;

/// <summary>
/// 自定义收款网关：客人扫商家自己的码，系统只记录，不处理资金。
/// 不找零（实收=应收），记录金额/方式/凭据号/备注/时间。
/// </summary>
public class CustomQrPaymentGateway : IPaymentGateway
{
    public string Name => "自定义收款";
    public bool IsEnabled => true;

    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Task.FromResult(PaymentResult.Fail("金额必须大于 0"));

        // 只记录，不处理资金。TransactionId 用本地生成的记录号。
        var recordId = $"CQR{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        return Task.FromResult(PaymentResult.Ok(recordId));
    }
}

/// <summary>
/// 微信支付网关（预留骨架，不实现业务）。
/// </summary>
public class WechatPayGateway : IPaymentGateway
{
    public string Name => "微信支付";
    public bool IsEnabled => false; // 未配置商户号

    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default)
        => Task.FromResult(PaymentResult.NotEnabled());
}

/// <summary>
/// 支付宝网关（预留骨架，不实现业务）。
/// </summary>
public class AlipayGateway : IPaymentGateway
{
    public string Name => "支付宝";
    public bool IsEnabled => false; // 未配置商户号

    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default)
        => Task.FromResult(PaymentResult.NotEnabled());
}

/// <summary>
/// 支付网关工厂：按配置选择网关，新增方式时不用改 UI 代码。
/// </summary>
public static class PaymentGatewayFactory
{
    private static readonly Dictionary<string, IPaymentGateway> _gateways = new()
    {
        ["custom"] = new CustomQrPaymentGateway(),
        ["wechat"] = new WechatPayGateway(),
        ["alipay"] = new AlipayGateway(),
    };

    /// <summary>按 key 获取网关。</summary>
    public static IPaymentGateway? Get(string key)
        => _gateways.TryGetValue(key.ToLowerInvariant(), out var g) ? g : null;

    /// <summary>所有已注册的网关（用于 UI 列表）。</summary>
    public static IReadOnlyList<(string Key, IPaymentGateway Gateway)> GetAll()
        => _gateways.Select(kv => (kv.Key, kv.Value)).ToList();

    /// <summary>注册新网关（扩展用）。</summary>
    public static void Register(string key, IPaymentGateway gateway)
        => _gateways[key.ToLowerInvariant()] = gateway;
}
