using Xunit;
using YiHua.Pos.Hardware.Printing.Templates;

namespace YiHua.Pos.Tests.Hardware.Templates;

public class TemplateVariableResolverTests
{
    private static Dictionary<string, object?> Vars() => new()
    {
        ["storeName"] = "逸华餐厅",
        ["orderNo"] = "A001",
        ["amountTotal"] = 128.5m,
        ["amountChange"] = 12m,
        ["createdAt"] = new DateTime(2026, 10, 12, 12, 30, 45),
        ["qty"] = 2,
    };

    [Fact]
    public void Resolve_Replaces_KnownVariables()
    {
        string result = TemplateVariableResolver.Resolve("门店:{storeName} 单号:{orderNo}", Vars());
        Assert.Equal("门店:逸华餐厅 单号:A001", result);
    }

    [Fact]
    public void Resolve_Keeps_UnknownVariables_AsIs()
    {
        string result = TemplateVariableResolver.Resolve("未知:{noSuchVar} 已知:{storeName}", Vars());
        Assert.Equal("未知:{noSuchVar} 已知:逸华餐厅", result);
    }

    [Fact]
    public void Resolve_Formats_Amount_TwoDecimals()
    {
        string result = TemplateVariableResolver.Resolve("合计:{amountTotal} 找零:{amountChange}", Vars());
        Assert.Equal("合计:128.50 找零:12.00", result);
    }

    [Fact]
    public void Resolve_Formats_Time()
    {
        string result = TemplateVariableResolver.Resolve("时间:{createdAt}", Vars());
        Assert.Equal("时间:2026-10-12 12:30:45", result);
    }

    [Fact]
    public void Resolve_Empty_Template_Returns_Empty()
    {
        Assert.Equal(string.Empty, TemplateVariableResolver.Resolve("", Vars()));
        Assert.Equal(string.Empty, TemplateVariableResolver.Resolve(null, Vars()));
    }

    [Fact]
    public void Resolve_NullVars_Keeps_Template()
    {
        Assert.Equal("{storeName}", TemplateVariableResolver.Resolve("{storeName}", null));
    }

    [Fact]
    public void FormatAmount_Handles_VariousInputs()
    {
        Assert.Equal("128.50", TemplateVariableResolver.FormatAmount(128.5m));
        Assert.Equal("0.00", TemplateVariableResolver.FormatAmount(0));
        Assert.Equal(string.Empty, TemplateVariableResolver.FormatAmount(null));
    }

    [Fact]
    public void Malformed_Braces_Kept_AsIs()
    {
        // 非变量格式的大括号原样保留
        Assert.Equal("价格{100}", TemplateVariableResolver.Resolve("价格{100}", Vars()));
    }
}
