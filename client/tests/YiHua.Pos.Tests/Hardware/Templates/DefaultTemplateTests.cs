using Xunit;
using YiHua.Pos.Hardware.Printing.Templates;
using YiHua.Pos.Hardware.Printing.Templates.Elements;

namespace YiHua.Pos.Tests.Hardware.Templates;

public class DefaultTemplateTests
{
    private static Dictionary<string, object?> FullVars() => new()
    {
        ["storeName"] = "逸华餐厅",
        ["storePhone"] = "010-88889999",
        ["storeAddress"] = "朝阳区美食街1号",
        ["orderNo"] = "A001",
        ["takeNo"] = "12",
        ["deskName"] = "A08",
        ["createdAt"] = new DateTime(2026, 10, 12, 12, 30, 0),
        ["settleTime"] = new DateTime(2026, 10, 12, 13, 5, 0),
        ["operatorName"] = "张三",
        ["payTypeName"] = "微信支付",
        ["remark"] = "少辣",
        ["qrContent"] = "https://yihua/pay/A001",
        ["amountTotal"] = 72m,
        ["amountDiscount"] = 0m,
        ["amountPay"] = 72m,
        ["amountReceived"] = 100m,
        ["amountChange"] = 28m,
        ["itemName"] = "珍珠奶茶",
        ["spec"] = "大杯/半糖/去冰",
        ["shiftName"] = "午市",
        ["shiftBegin"] = new DateTime(2026, 10, 12, 10, 0, 0),
        ["shiftEnd"] = new DateTime(2026, 10, 12, 14, 0, 0),
        ["orderCount"] = 36,
        ["cashExpected"] = 1250m,
        ["cashActual"] = 1250m,
        ["cashDiff"] = 0m,
        ["items"] = new List<PrintItemRow>
        {
            new() { Name = "宫保鸡丁", Qty = 2, Price = 32m, Amount = 64m, Remark = "少辣" },
            new() { Name = "珍珠奶茶", Qty = 1, Price = 8m, Amount = 8m, Spec = "大杯" },
        },
        ["payTypes"] = new List<IReadOnlyList<string>>
        {
            new List<string> { "微信支付", "20", "860.00" },
            new List<string> { "现金", "16", "390.00" },
        },
    };

    [Theory]
    [InlineData(PaperWidth.Mm80)]
    [InlineData(PaperWidth.Mm58)]
    public void Receipt_Renders_NonEmpty(PaperWidth paper)
    {
        byte[] bytes = new TemplateRenderer(TemplateDefaults.Receipt(paper)).Render(FullVars());
        Assert.NotEmpty(bytes);
        string preview = new TemplateRenderer(TemplateDefaults.Receipt(paper)).RenderPreview(FullVars());
        Assert.Contains("逸华餐厅", preview);
        Assert.Contains("A001", preview);
        Assert.Contains("72.00", preview);
    }

    [Fact]
    public void Kitchen_Has_No_Amount()
    {
        string preview = new TemplateRenderer(TemplateDefaults.Kitchen()).RenderPreview(FullVars());
        Assert.NotEmpty(preview);
        Assert.Contains("A08", preview);
        Assert.Contains("宫保鸡丁", preview);
        Assert.Contains("少辣", preview);
        // 后厨单不放金额
        Assert.DoesNotContain("64.00", preview);
        Assert.DoesNotContain("72.00", preview);
    }

    [Fact]
    public void Label_Renders_TakeNo_Big()
    {
        var template = TemplateDefaults.Label();
        Assert.Equal(PaperWidth.Mm58, template.Paper); // 标签默认 58mm

        string preview = new TemplateRenderer(template).RenderPreview(FullVars());
        Assert.Contains("12", preview);
        Assert.Contains("珍珠奶茶", preview);
        Assert.Contains("大杯/半糖/去冰", preview);
    }

    [Fact]
    public void Shift_Renders_PayTypes_And_CashCheck()
    {
        string preview = new TemplateRenderer(TemplateDefaults.Shift()).RenderPreview(FullVars());
        Assert.Contains("交班单", preview);
        Assert.Contains("微信支付", preview);
        Assert.Contains("860.00", preview);
        Assert.Contains("1250.00", preview);
    }

    [Fact]
    public void Receipt_58mm_Lines_Fit_32_Columns()
    {
        string preview = new TemplateRenderer(TemplateDefaults.Receipt(PaperWidth.Mm58)).RenderPreview(FullVars());
        foreach (string line in preview.Split('\n'))
        {
            if (line.StartsWith("[")) continue; // QR 占位行跳过
            Assert.True(DisplayWidth.Of(line.TrimEnd()) <= 32, $"超宽行: {line}");
        }
    }
}
