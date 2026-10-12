using YiHua.Pos.UI.Services.Member;
using Xunit;

namespace YiHua.Pos.Tests.UI.Member;

public class RechargeValidatorTests
{
    [Fact]
    public void Validate_Over5000_Rejected()
    {
        Assert.False(RechargeValidator.Validate(5001m, 5000m, 0m, out var error));
        Assert.Contains("5000", error);
        Assert.Contains("预付卡", error); // 合规提示
    }

    [Fact]
    public void Validate_Exactly5000_Allowed()
    {
        Assert.True(RechargeValidator.Validate(5000m, 5000m, 0m, out _));
    }

    [Fact]
    public void Validate_ZeroOrNegative_Rejected()
    {
        Assert.False(RechargeValidator.Validate(0m, 5000m, 0m, out _));
        Assert.False(RechargeValidator.Validate(-100m, 5000m, 0m, out _));
    }

    [Fact]
    public void Validate_BalanceExceedsLimit_Rejected()
    {
        // 当前 4000，再充 1500 → 5500，超上限
        Assert.False(RechargeValidator.Validate(1500m, 5000m, 4000m, out var error));
        Assert.Contains("超过", error);
    }

    [Fact]
    public void ValidateUpperLimit_CannotExceed5000()
    {
        Assert.False(RechargeValidator.ValidateUpperLimit(6000m, out var error));
        Assert.Contains("5000", error);
        // 可以调低（地方更严）
        Assert.True(RechargeValidator.ValidateUpperLimit(3000m, out _));
    }
}

public class BalanceAdjustTests
{
    private static MemberDto MakeMember(decimal balance) => new()
    {
        Id = 1, Name = "张三", Phone = "13812345678", Balance = balance,
    };

    [Fact]
    public void Adjust_Add_Allowed()
    {
        var result = BalanceAdjuster.Adjust(
            MakeMember(100m), 10m, BalanceChangeReason.Compensation, "老板");
        Assert.True(result.Success);
        Assert.Equal(110m, result.NewBalance);
        Assert.NotNull(result.Record);
        Assert.Equal(10m, result.Record.Amount);
        Assert.Equal("补偿", result.Record.ReasonText);
    }

    [Fact]
    public void Adjust_Subtract_Allowed()
    {
        var result = BalanceAdjuster.Adjust(
            MakeMember(100m), -5m, BalanceChangeReason.Correction, "老板");
        Assert.True(result.Success);
        Assert.Equal(95m, result.NewBalance);
    }

    [Fact]
    public void Adjust_ToNegative_Rejected()
    {
        var result = BalanceAdjuster.Adjust(
            MakeMember(10m), -20m, BalanceChangeReason.Correction, "老板");
        Assert.False(result.Success);
        Assert.Contains("不能为负", result.Error);
    }

    [Fact]
    public void Adjust_NoReason_Rejected()
    {
        var result = BalanceAdjuster.Adjust(
            MakeMember(100m), 10m, null, "老板");
        Assert.False(result.Success);
        Assert.Contains("原因", result.Error);
    }

    [Fact]
    public void Adjust_Zero_Rejected()
    {
        var result = BalanceAdjuster.Adjust(
            MakeMember(100m), 0m, BalanceChangeReason.Gift, "老板");
        Assert.False(result.Success);
    }
}

public class MemberPhoneMaskerTests
{
    [Fact]
    public void Mask_Correct()
    {
        Assert.Equal("138****5678", MemberPhoneMasker.Mask("13812345678"));
    }

    [Fact]
    public void Mask_Empty_ReturnsEmpty()
    {
        Assert.Equal("", MemberPhoneMasker.Mask(""));
        Assert.Equal("", MemberPhoneMasker.Mask(null));
    }

    [Fact]
    public void IsValid_Correct()
    {
        Assert.True(MemberPhoneMasker.IsValid("13812345678", out _));
    }

    [Fact]
    public void IsValid_Invalid()
    {
        Assert.False(MemberPhoneMasker.IsValid("", out var e1));
        Assert.Contains("不能为空", e1);
        Assert.False(MemberPhoneMasker.IsValid("123", out _));
        Assert.False(MemberPhoneMasker.IsValid("23812345678", out _)); // 不是1开头
    }
}
