namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>
/// 券码归一化：去空格、转大写。
/// 扫码枪前缀后缀过滤：部分扫码枪带 F9 触发 / 回车尾 / 自定义前后缀。
/// </summary>
public static class CouponInputNormalizer
{
    /// <summary>
    /// 归一化：去首尾空白、去内部空白、转大写。
    /// 二维码解码结果可能带空格/换行。
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;
        // 去掉所有空白字符（空格/换行/制表符）
        var cleaned = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return cleaned.ToUpperInvariant();
    }

    /// <summary>
    /// 过滤扫码枪前后缀。
    /// 常见：前缀如 "F9" 触发键、后缀回车（\r\n）。
    /// prefix/suffix 为配置的前后缀，空则不处理。
    /// </summary>
    public static string StripAffixes(string? input, string? prefix, string? suffix)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        var s = input;
        if (!string.IsNullOrEmpty(prefix) && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            s = s[prefix.Length..];
        if (!string.IsNullOrEmpty(suffix) && s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            s = s[..^suffix.Length];
        // 扫码枪回车尾：\r \n
        s = s.TrimEnd('\r', '\n');
        return s;
    }

    /// <summary>
    /// 完整处理：去前后缀 → 归一化。
    /// </summary>
    public static string Process(string? raw, string? prefix = null, string? suffix = null) =>
        Normalize(StripAffixes(raw, prefix, suffix));
}
