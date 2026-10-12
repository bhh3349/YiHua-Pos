using YiHua.Pos.UI.Services.GroupBuy;
using Xunit;

namespace YiHua.Pos.Tests.UI.GroupBuy;

/// <summary>双层 JSON 解包：data 里套 JSON 字符串；无效 JSON 不崩</summary>
public class DoubleEnvelopeTests
{
    [Fact]
    public void ParseVerify_StringData_Unpacks()
    {
        // 双层：data 是 JSON 字符串
        string inner = "{\"valid\":true,\"coupon\":{\"couponCode\":\"MT123\",\"platformCode\":\"meituan\",\"couponName\":\"30元券\",\"faceValue\":\"30\",\"discountAmount\":\"30\",\"validFrom\":\"\",\"validTo\":\"\"}}";
        string json = "{\"code\":\"200\",\"message\":\"ok\",\"data\":\"" + inner.Replace("\"", "\\\"") + "\"}";
        var result = HttpGroupBuyClient.ParseVerify(json);
        Assert.True(result.Valid);
        Assert.Equal("MT123", result.Coupon!.CouponCode);
        Assert.Equal(30m, result.Coupon.DiscountAmount);
    }

    [Fact]
    public void ParseVerify_ObjectData_Unpacks()
    {
        // data 直接是对象（兼容）
        string json = "{\"code\":\"200\",\"message\":\"ok\",\"data\":{\"valid\":true,\"coupon\":{\"couponCode\":\"MT123\",\"platformCode\":\"meituan\",\"faceValue\":\"50\",\"discountAmount\":\"30\"}}}";
        var result = HttpGroupBuyClient.ParseVerify(json);
        Assert.True(result.Valid);
        Assert.Equal(30m, result.Coupon!.DiscountAmount); // 用 discountAmount
        Assert.Equal(50m, result.Coupon.FaceValue);
    }

    [Fact]
    public void ParseVerify_ErrorCode_Mapped()
    {
        string json = "{\"code\":\"200\",\"message\":\"ok\",\"data\":{\"valid\":false,\"errorCode\":\"COUPON_EXPIRED\",\"message\":\"expired\",\"coupon\":null}}";
        var result = HttpGroupBuyClient.ParseVerify(json);
        Assert.False(result.Valid);
        Assert.Equal("COUPON_EXPIRED", result.ErrorCode);
    }

    [Fact]
    public void ParseVerify_Non200_Code()
    {
        string json = "{\"code\":\"500\",\"message\":\"server error\"}";
        var result = HttpGroupBuyClient.ParseVerify(json);
        Assert.False(result.Valid);
        Assert.Equal("500", result.ErrorCode);
    }

    [Fact]
    public void ParseVerify_InvalidJson_NoCrash()
    {
        var result = HttpGroupBuyClient.ParseVerify("not json {{{");
        Assert.False(result.Valid);
        Assert.Equal("BAD_JSON", result.ErrorCode);
    }

    [Fact]
    public void ParseVerify_MissingData_NoCrash()
    {
        var result = HttpGroupBuyClient.ParseVerify("{\"code\":\"200\"}");
        Assert.False(result.Valid);
        Assert.Equal("BAD_RESPONSE", result.ErrorCode);
    }
}
