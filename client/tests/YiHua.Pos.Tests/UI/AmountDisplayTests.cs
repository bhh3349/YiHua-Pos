using YiHua.Pos.UI.Controls.Display;
using Xunit;

namespace YiHua.Pos.Tests.UI;

/// <summary>
/// AmountDisplay 金额拆分逻辑测试（签名控件的核心）。
/// </summary>
public class AmountDisplayTests
{
    [Theory]
    [InlineData(128.00, 2, "128", "00")]
    [InlineData(128.5, 2, "128", "50")]
    [InlineData(0, 2, "0", "00")]
    [InlineData(9.999, 2, "10", "00")] // 四舍五入进位
    [InlineData(1234.56, 0, "1,235", "")] // 0 位小数
    public void SplitAmount_CorrectParts(double amountDouble, int places, string expectedInt, string expectedFrac)
    {
        var amount = (decimal)amountDouble;
        var (integer, fraction) = AmountDisplay.SplitAmount(amount, places);
        Assert.Equal(expectedInt, integer);
        Assert.Equal(expectedFrac, fraction);
    }

    [Fact]
    public void SplitAmount_Negative_HandlesSign()
    {
        var (integer, fraction) = AmountDisplay.SplitAmount(-5.25m, 2);
        Assert.Equal("-5", integer);
        Assert.Equal("25", fraction);
    }

    [Fact]
    public void SplitAmount_ThousandSeparator()
    {
        var (integer, _) = AmountDisplay.SplitAmount(1234567.89m, 2);
        Assert.Equal("1,234,567", integer); // 千分位，大金额易读
    }

    [Fact]
    public void DefaultProperties_Sane()
    {
        var display = new AmountDisplay();
        Assert.Equal(2, display.DecimalPlaces);
        Assert.Equal("¥", display.CurrencySymbol);
    }

    [Fact]
    public void SettingAmount_DoesNotThrow_Headless()
    {
        var display = new AmountDisplay { Amount = 128.00m };
        Assert.Equal(128.00m, display.Amount);
    }
}
