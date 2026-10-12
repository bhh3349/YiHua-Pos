using YiHua.Pos.UI.Services.GroupBuy;
using Xunit;

namespace YiHua.Pos.Tests.UI.GroupBuy;

/// <summary>8 种错误码各自不同的人话</summary>
public class CouponErrorMapperTests
{
    [Theory]
    [InlineData("COUPON_USED", "已经用过")]
    [InlineData("COUPON_EXPIRED", "已过期")]
    [InlineData("COUPON_STORE_MISMATCH", "指定门店")]
    [InlineData("COUPON_TIME_RESTRICTED", "特定时段")]
    [InlineData("COUPON_NEED_BOOKING", "预约")]
    [InlineData("COUPON_NOT_FOUND", "不存在")]
    [InlineData("COUPON_NOT_YET_VALID", "还没到")]
    [InlineData("PLATFORM_MISMATCH", "不属于该平台")]
    public void Map_Each_Code_Has_Human_Text(string code, string keyword)
    {
        string text = CouponErrorMapper.Map(code, null);
        Assert.Contains(keyword, text);
        // 不是生硬的错误码
        Assert.DoesNotContain(code, text);
    }

    [Fact]
    public void Map_Unknown_Code_Falls_Back_To_Server_Message()
    {
        string text = CouponErrorMapper.Map("SOME_NEW_CODE", "服务端返回的中文");
        Assert.Equal("服务端返回的中文", text);
    }

    [Fact]
    public void Map_Unknown_Code_No_Message_Generic()
    {
        string text = CouponErrorMapper.Map("SOME_NEW_CODE", null);
        Assert.Contains("失败", text);
    }
}
