using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void ValidateStoreName_Empty_Rejected()
    {
        Assert.False(SettingsValidator.ValidateStoreName("", out var error));
        Assert.Contains("不能为空", error);
        Assert.False(SettingsValidator.ValidateStoreName("   ", out _));
        Assert.False(SettingsValidator.ValidateStoreName(null, out _));
    }

    [Fact]
    public void ValidateStoreName_TooLong_Rejected()
    {
        var longName = new string('店', 31);
        Assert.False(SettingsValidator.ValidateStoreName(longName, out var error));
        Assert.Contains("太长", error);
    }

    [Fact]
    public void ValidateStoreName_Valid_Passes()
    {
        Assert.True(SettingsValidator.ValidateStoreName("老王牛肉面", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrinters_Empty_Rejected()
    {
        Assert.False(SettingsValidator.ValidatePrinters(new List<PrinterConfigDto>(), out var error));
        Assert.Contains("打印机", error);
        Assert.False(SettingsValidator.ValidatePrinters(null, out _));
    }

    [Fact]
    public void ValidatePrinters_NoReceiptPrinter_Rejected()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "后厨打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp0" },
        };
        Assert.False(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Contains("小票", error);
    }

    [Fact]
    public void ValidatePrinters_Valid_Passes()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "前台小票机", Usage = PrinterUsage.Receipt, DevicePath = "/dev/usb/lp0" },
            new() { Name = "后厨打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp1", Station = "热菜" },
        };
        Assert.True(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrinters_MissingName_Rejected()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "", Usage = PrinterUsage.Receipt, DevicePath = "/dev/usb/lp0" },
        };
        Assert.False(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Contains("名称", error);
    }

    [Fact]
    public void ValidateServerAddress_Invalid_Rejected()
    {
        Assert.False(SettingsValidator.ValidateServerAddress("", out _));
        Assert.False(SettingsValidator.ValidateServerAddress("not-a-url", out var error));
        Assert.Contains("http", error);
    }

    [Fact]
    public void ValidateServerAddress_Valid_Passes()
    {
        Assert.True(SettingsValidator.ValidateServerAddress("http://192.168.1.100:8000", out _));
        Assert.True(SettingsValidator.ValidateServerAddress("https://api.example.com", out _));
    }
}

public class RoundingCalculatorTests
{
    [Fact]
    public void ApplyRounding_None_KeepsCents()
    {
        Assert.Equal(38.50m, RoundingCalculator.ApplyRounding(38.5m, RoundingRule.None));
    }

    [Fact]
    public void ApplyRounding_ToJiao_向下抹掉分()
    {
        // 抹零是「抹掉」，不是四舍五入：
        // 38.54 抹到角是 38.50；38.59 也是 38.50（不是 38.60）
        Assert.Equal(38.5m, RoundingCalculator.ApplyRounding(38.54m, RoundingRule.ToJiao));
        Assert.Equal(38.5m, RoundingCalculator.ApplyRounding(38.59m, RoundingRule.ToJiao));
        Assert.Equal(38.5m, RoundingCalculator.ApplyRounding(38.5m, RoundingRule.ToJiao));
    }

    [Fact]
    public void ApplyRounding_ToYuan_向下抹掉角()
    {
        // 38.5 抹到元是 38，不是 39 —— 往上凑整等于多收客人钱
        Assert.Equal(38m, RoundingCalculator.ApplyRounding(38.4m, RoundingRule.ToYuan));
        Assert.Equal(38m, RoundingCalculator.ApplyRounding(38.5m, RoundingRule.ToYuan));
        Assert.Equal(38m, RoundingCalculator.ApplyRounding(38.99m, RoundingRule.ToYuan));
    }

    [Fact]
    public void ApplyRounding_抹零不会让金额变大()
    {
        // 无论哪个规则，抹零后都不可能比原价高
        foreach (var amount in new[] { 0.01m, 9.99m, 38.55m, 128.57m, 999.99m })
        {
            foreach (var rule in new[] { RoundingRule.ToJiao, RoundingRule.ToYuan })
            {
                var rounded = RoundingCalculator.ApplyRounding(amount, rule);
                Assert.True(rounded <= amount, $"{amount} 抹零后变成 {rounded}，比原价还高");
            }
        }
    }

    [Fact]
    public void Preview_ShowsRuleAndResult()
    {
        var preview = RoundingCalculator.Preview(38.5m, RoundingRule.ToYuan);
        Assert.Contains("抹到元", preview);
        Assert.Contains("38", preview); // 38.5 向下抹到元 → 38
    }
}
