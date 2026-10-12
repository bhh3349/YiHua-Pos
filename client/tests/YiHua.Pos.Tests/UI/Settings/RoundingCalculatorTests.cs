using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class RoundingCalculatorTests
{
    [Fact]
    public void ApplyRounding_None_KeepsCents()
    {
        Assert.Equal(38.50m, RoundingCalculator.ApplyRounding(38.5m, RoundingRule.None));
    }

    [Fact]
    public void ApplyRounding_ToJiao()
    {
        Assert.Equal(38.5m, RoundingCalculator.ApplyRounding(38.54m, RoundingRule.ToJiao));
        Assert.Equal(38.6m, RoundingCalculator.ApplyRounding(38.55m, RoundingRule.ToJiao)); // AwayFromZero
    }

    [Fact]
    public void ApplyRounding_ToYuan()
    {
        Assert.Equal(38m, RoundingCalculator.ApplyRounding(38.4m, RoundingRule.ToYuan));
        Assert.Equal(39m, RoundingCalculator.ApplyRounding(38.5m, RoundingRule.ToYuan)); // AwayFromZero
    }

    [Fact]
    public void Preview_ShowsRuleAndResult()
    {
        var preview = RoundingCalculator.Preview(38.5m, RoundingRule.ToYuan);
        Assert.Contains("抹到元", preview);
        Assert.Contains("39", preview); // 38.5 AwayFromZero → 39
    }
}
