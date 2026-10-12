using System.Net.Http.Json;
using System.Text.Json;

namespace YiHua.Pos.UI.Services.GroupBuy;

/// <summary>
/// HTTP 实现：POST /api/v1/groupbuy/coupon/verify。
/// 双层封装：data 字段里再套一层 JSON 字符串，必须反序列化两次。
/// 无效 JSON / 双层缺一层 → 不崩，返回明确错误。
/// </summary>
public class HttpGroupBuyClient : IGroupBuyClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public HttpGroupBuyClient(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<CouponVerifyResult> VerifyAsync(string platformCode, string couponCode, long storeId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/api/v1/groupbuy/coupon/verify",
                new { platformCode, couponCode, storeId }, ct).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseVerify(json);
        }
        catch (HttpRequestException ex)
        {
            // 断网：返回特殊标记，调用方提示"按券面金额直接打折"
            return new CouponVerifyResult { Valid = false, ErrorCode = "NETWORK_ERROR", Message = ex.Message };
        }
        catch (Exception ex)
        {
            return new CouponVerifyResult { Valid = false, ErrorCode = "CLIENT_ERROR", Message = ex.Message };
        }
    }

    /// <summary>
    /// 双层 JSON 解包：{"code":"200","data":"{...}"} 或 {"code":"200","data":{...}}。
    /// </summary>
    public static CouponVerifyResult ParseVerify(string json)
    {
        try
        {
            using var outer = JsonDocument.Parse(json);
            var root = outer.RootElement;

            string? code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code != "200")
            {
                string msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                return new CouponVerifyResult { Valid = false, ErrorCode = code ?? "UNKNOWN", Message = msg };
            }

            if (!root.TryGetProperty("data", out var dataProp))
                return new CouponVerifyResult { Valid = false, ErrorCode = "BAD_RESPONSE", Message = "服务端返回缺少 data" };

            // 双层：data 可能是 JSON 字符串，也可能是直接的对象
            JsonDocument inner;
            bool disposeInner = false;
            if (dataProp.ValueKind == JsonValueKind.String)
            {
                inner = JsonDocument.Parse(dataProp.GetString() ?? "{}");
                disposeInner = true;
            }
            else if (dataProp.ValueKind == JsonValueKind.Object)
            {
                inner = JsonDocument.Parse(dataProp.GetRawText());
                disposeInner = true;
            }
            else
            {
                return new CouponVerifyResult { Valid = false, ErrorCode = "BAD_RESPONSE", Message = "data 格式异常" };
            }

            using (inner)
            {
                var data = inner.RootElement;
                bool valid = data.TryGetProperty("valid", out var v) && v.GetBoolean();
                if (!valid)
                {
                    string? ec = data.TryGetProperty("errorCode", out var e) ? e.GetString() : null;
                    string? msg = data.TryGetProperty("message", out var mm) ? mm.GetString() : null;
                    return new CouponVerifyResult { Valid = false, ErrorCode = ec, Message = msg };
                }

                if (!data.TryGetProperty("coupon", out var couponProp) || couponProp.ValueKind != JsonValueKind.Object)
                    return new CouponVerifyResult { Valid = false, ErrorCode = "BAD_RESPONSE", Message = "缺少券信息" };

                return new CouponVerifyResult { Valid = true, Coupon = ParseCoupon(couponProp) };
            }
        }
        catch (JsonException ex)
        {
            return new CouponVerifyResult { Valid = false, ErrorCode = "BAD_JSON", Message = $"响应解析失败：{ex.Message}" };
        }
    }

    private static CouponInfo ParseCoupon(JsonElement c)
    {
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var p) ? p.GetString() ?? "" : "";
        static decimal D(JsonElement e, string n)
        {
            if (!e.TryGetProperty(n, out var p)) return 0m;
            var s = p.ValueKind == JsonValueKind.String ? p.GetString() : p.GetRawText();
            return decimal.TryParse(s, out var d) ? d : 0m;
        }
        return new CouponInfo
        {
            CouponCode = S(c, "couponCode"),
            PlatformCode = S(c, "platformCode"),
            CouponName = S(c, "couponName"),
            FaceValue = D(c, "faceValue"),
            DiscountAmount = D(c, "discountAmount"),
            ValidFrom = S(c, "validFrom"),
            ValidTo = S(c, "validTo"),
        };
    }

    public async Task<CouponRedeemResult> RedeemAsync(string platformCode, string couponCode, long storeId, long orderId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/api/v1/groupbuy/coupon/redeem",
                new { platformCode, couponCode, storeId, orderId }, ct).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.GetString() == "200")
                return new CouponRedeemResult { Success = true };
            return new CouponRedeemResult
            {
                Success = false,
                ErrorCode = root.TryGetProperty("code", out var ec) ? ec.GetString() : "UNKNOWN",
                Message = root.TryGetProperty("message", out var m) ? m.GetString() : null,
            };
        }
        catch (Exception ex)
        {
            return new CouponRedeemResult { Success = false, ErrorCode = "CLIENT_ERROR", Message = ex.Message };
        }
    }

    public async Task<bool> ReverseAsync(string platformCode, string couponCode, long storeId, string reason, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/api/v1/groupbuy/coupon/reverse",
                new { platformCode, couponCode, storeId, reason }, ct).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("code", out var c) && c.GetString() == "200";
        }
        catch
        {
            return false;
        }
    }
}
