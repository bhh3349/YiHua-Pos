namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>假实现（测试/离线开发用）</summary>
public class FakeGroupBuyClient : IGroupBuyClient
{
    private readonly Dictionary<string, CouponInfo> _coupons = new();
    private readonly HashSet<string> _redeemed = new();

    public void AddCoupon(CouponInfo coupon) => _coupons[coupon.CouponCode] = coupon;

    public Task<CouponVerifyResult> VerifyAsync(string platformCode, string couponCode, long storeId, CancellationToken ct = default)
    {
        if (!_coupons.TryGetValue(couponCode, out var coupon))
            return Task.FromResult(new CouponVerifyResult { Valid = false, ErrorCode = "COUPON_NOT_FOUND" });
        if (_redeemed.Contains(couponCode))
            return Task.FromResult(new CouponVerifyResult { Valid = false, ErrorCode = "COUPON_USED" });
        return Task.FromResult(new CouponVerifyResult { Valid = true, Coupon = coupon });
    }

    public Task<CouponRedeemResult> RedeemAsync(string platformCode, string couponCode, long storeId, long orderId, CancellationToken ct = default)
    {
        // 幂等：已核销返回成功，不重复扣
        _redeemed.Add(couponCode);
        decimal discount = _coupons.TryGetValue(couponCode, out var c) ? c.DiscountAmount : 0m;
        return Task.FromResult(new CouponRedeemResult { Success = true, ActualDiscount = discount });
    }

    public Task<bool> ReverseAsync(string platformCode, string couponCode, long storeId, string reason, CancellationToken ct = default)
    {
        _redeemed.Remove(couponCode);
        return Task.FromResult(true);
    }
}
