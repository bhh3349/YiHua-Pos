using YiHua.Pos.Hardware.Platforms.Linux;
using Xunit;

namespace YiHua.Pos.Tests.Hardware.Linux;

/// <summary>udev 规则生成器测试：断言输出文本内容正确。</summary>
public class UdevRulesGeneratorTests
{
    [Fact]
    public void Generate_Includes_VidPid_And_Device_Rules()
    {
        string rules = UdevRulesGenerator.Generate(new[] { ("0416", "5011") });

        Assert.Contains("0416", rules);
        Assert.Contains("5011", rules);
        Assert.Contains("ATTRS{idVendor}", rules);
        Assert.Contains("KERNEL==\"ttyUSB*\"", rules);
        Assert.Contains("KERNEL==\"ttyACM*\"", rules);
        Assert.Contains("KERNEL==\"lp*\"", rules);
        Assert.Contains("MODE=\"0666\"", rules);
        Assert.Contains($"GROUP=\"{UdevRulesGenerator.DefaultGroup}\"", rules);
    }

    [Fact]
    public void Generate_Without_KnownPrinters_Omits_VidPid_Section()
    {
        string rules = UdevRulesGenerator.Generate();

        Assert.DoesNotContain("idVendor", rules);
        // 通用串口规则仍在
        Assert.Contains("KERNEL==\"ttyUSB*\"", rules);
    }

    [Fact]
    public void Generate_Normalizes_VidPid_To_Lowercase()
    {
        string rules = UdevRulesGenerator.Generate(new[] { ("0416", "AB12") });

        Assert.Contains("ab12", rules);
        Assert.DoesNotContain("AB12", rules);
    }

    [Fact]
    public void Generate_Supports_Custom_Group()
    {
        string rules = UdevRulesGenerator.Generate(new[] { ("0416", "5011") }, group: "printers");

        Assert.Contains("GROUP=\"printers\"", rules);
        Assert.DoesNotContain($"GROUP=\"{UdevRulesGenerator.DefaultGroup}\"", rules);
    }

    [Fact]
    public void Generate_Multiple_Printers_Each_Get_A_Rule()
    {
        string rules = UdevRulesGenerator.Generate(new[] { ("0416", "5011"), ("0fe6", "811e") });

        Assert.Contains("0416", rules);
        Assert.Contains("0fe6", rules);
    }
}
