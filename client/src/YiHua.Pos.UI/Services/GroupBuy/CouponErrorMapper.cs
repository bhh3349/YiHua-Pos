namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>
/// 错误码 → 收银员看得懂的话。
/// 收银员看到「券无效」没法处理客人，看到「只能在午市用」才知道该怎么说。
/// </summary>
public static class CouponErrorMapper
{
    public static string Map(string? code, string? serverMessage)
    {
        return code switch
        {
            "COUPON_USED" => "这张券已经用过了，让客人出示新的券",
            "COUPON_EXPIRED" => "券已过期，请客人确认有效期",
            "COUPON_STORE_MISMATCH" => "这张券只能在指定门店使用",
            "COUPON_TIME_RESTRICTED" => "这张券只能在特定时段用（如仅限午市）",
            "COUPON_NEED_BOOKING" => "这张券需要提前预约",
            "COUPON_NOT_FOUND" => "券码不存在，请核对后重试",
            "COUPON_NOT_YET_VALID" => "券还没到使用时间",
            "PLATFORM_MISMATCH" => "这张券不属于该平台",
            _ => string.IsNullOrWhiteSpace(serverMessage)
                ? "券验证失败，请重试"
                : serverMessage,
        };
    }
}
