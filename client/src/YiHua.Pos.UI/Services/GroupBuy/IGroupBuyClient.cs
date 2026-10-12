namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>团购服务端客户端接口</summary>
public interface IGroupBuyClient
{
    /// <summary>验券（只查不动钱）</summary>
    Task<CouponVerifyResult> VerifyAsync(string platformCode, string couponCode, long storeId, CancellationToken ct = default);

    /// <summary>核销（幂等）</summary>
    Task<CouponRedeemResult> RedeemAsync(string platformCode, string couponCode, long storeId, long orderId, CancellationToken ct = default);

    /// <summary>撤销核销</summary>
    Task<bool> ReverseAsync(string platformCode, string couponCode, long storeId, string reason, CancellationToken ct = default);
}
