using YiHua.Pos.UI.Services.GroupBuy;
using Xunit;

namespace YiHua.Pos.Tests.UI.GroupBuy;

/// <summary>扫码枪过滤 + 归一化</summary>
public class CouponInputNormalizerTests
{
    [Fact]
    public void Normalize_Strips_Whitespace_And_Upper()
    {
        // 二维码解码带空格/换行/小写
        Assert.Equal("MT123ABC", CouponInputNormalizer.Normalize(" mt123abc \n"));
    }

    [Fact]
    public void Normalize_Removes_Inner_Spaces()
    {
        Assert.Equal("ABC123", CouponInputNormalizer.Normalize("AB C\n1 23"));
    }

    [Fact]
    public void Normalize_Empty_Returns_Empty()
    {
        Assert.Equal(string.Empty, CouponInputNormalizer.Normalize(null));
        Assert.Equal(string.Empty, CouponInputNormalizer.Normalize("   "));
    }

    [Fact]
    public void StripAffixes_Removes_Prefix_Suffix()
    {
        // 扫码枪前缀 F9，后缀回车
        Assert.Equal("MT123", CouponInputNormalizer.StripAffixes("F9MT123", "F9", null));
        Assert.Equal("MT123", CouponInputNormalizer.StripAffixes("MT123\r\n", null, null));
    }

    [Fact]
    public void Process_Full_Pipeline()
    {
        Assert.Equal("MT123ABC",
            CouponInputNormalizer.Process("F9 mt123abc \r\n", "F9", null));
    }

    [Fact]
    public void StripAffixes_No_Affix_Unchanged()
    {
        Assert.Equal("MT123", CouponInputNormalizer.StripAffixes("MT123", null, null));
    }
}
