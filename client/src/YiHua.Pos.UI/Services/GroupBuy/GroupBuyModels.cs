namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>
/// 团购券信息（对应服务端 CouponInfo.to_dict）。
/// 金额一律 string 传输，客户端用 decimal 解析。
/// </summary>
public class CouponInfo
{
    public string CouponCode { get; set; } = string.Empty;
    public string PlatformCode { get; set; } = string.Empty;
    public string CouponName { get; set; } = string.Empty;

    /// <summary>面值（如满 30 券，面值 30）</summary>
    public decimal FaceValue { get; set; }

    /// <summary>本单实际抵扣（用这个，不用面值）</summary>
    public decimal DiscountAmount { get; set; }

    public string ValidFrom { get; set; } = string.Empty;
    public string ValidTo { get; set; } = string.Empty;

    /// <summary>平台中文名（美团/饿了么/抖音），不是 meituan</summary>
    public string PlatformName => PlatformNames.ToChinese(PlatformCode);
}

/// <summary>验券结果</summary>
public class CouponVerifyResult
{
    public bool Valid { get; set; }
    public CouponInfo? Coupon { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>核销结果</summary>
public class CouponRedeemResult
{
    public bool Success { get; set; }
    public string? RedeemId { get; set; }
    public decimal ActualDiscount { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>平台中文名映射</summary>
public static class PlatformNames
{
    public static string ToChinese(string code) => code.ToLowerInvariant() switch
    {
        "meituan" => "美团",
        "eleme" => "饿了么",
        "douyin" => "抖音",
        _ => code,
    };
}
