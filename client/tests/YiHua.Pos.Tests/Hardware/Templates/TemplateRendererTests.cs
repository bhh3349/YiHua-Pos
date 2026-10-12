using Xunit;
using YiHua.Pos.Hardware.Printing.EscPos;
using YiHua.Pos.Hardware.Printing.Templates;
using YiHua.Pos.Hardware.Printing.Templates.Elements;

namespace YiHua.Pos.Tests.Hardware.Templates;

public class TemplateRendererTests
{
    private static Dictionary<string, object?> SampleVars() => new()
    {
        ["storeName"] = "逸华餐厅",
        ["orderNo"] = "A001",
        ["amountTotal"] = 128.5m,
        ["items"] = new List<PrintItemRow>
        {
            new() { Name = "宫保鸡丁", Qty = 2, Price = 32m, Amount = 64m },
            new() { Name = "Cola", Qty = 1, Price = 8m, Amount = 8m },
        },
    };

    private static PrintTemplate TextTemplate() => new()
    {
        Name = "t",
        Paper = PaperWidth.Mm80,
        CutAfter = false,
        Elements = new List<TemplateElement>
        {
            new TextElement { Text = "门店:{storeName}" },
        },
    };

    [Fact]
    public void Render_Starts_With_Init_Command()
    {
        byte[] bytes = new TemplateRenderer(TextTemplate()).Render(SampleVars());
        // ESC @
        Assert.True(bytes.Length >= 2);
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);
    }

    [Fact]
    public void Render_Resolves_Variables_Into_Bytes()
    {
        byte[] bytes = new TemplateRenderer(TextTemplate()).Render(SampleVars());
        string gb = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        Assert.Contains("门店:逸华餐厅", gb);
    }

    [Fact]
    public void RenderPreview_Shows_Readable_Text()
    {
        string preview = new TemplateRenderer(TextTemplate()).RenderPreview(SampleVars());
        Assert.Contains("门店:逸华餐厅", preview);
        // 预览里不应有控制字符（char 重载走序号比较，避免文化的忽略权重问题）
        Assert.False(preview.Contains('\x1B'), "预览不应包含 ESC 控制码");
    }

    [Fact]
    public void Table_Aligns_Mixed_Chinese_And_Ascii()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm80,
            CutAfter = false,
            Elements = new List<TemplateElement>
            {
                new TableElement
                {
                    Columns = new[] { "商品", "数量", "金额" },
                    Widths = new[] { 28, 8, 12 },
                    Fields = new[] { "Name", "Qty", "Amount" },
                    DataSource = "items",
                },
            },
        };

        string preview = new TemplateRenderer(template).RenderPreview(SampleVars());
        // 按 \r\n 切分：只切 '\n' 会把 CR 留在行尾，DisplayWidth 会多算一列
        string[] lines = preview.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // 表头 + 2 行数据
        Assert.Equal(3, lines.Length);
        foreach (string line in lines)
            Assert.True(DisplayWidth.Of(line) <= 48, $"超宽行: {line}");

        // 数据行占满 48 列（金额右对齐，无尾部空格可裁）
        Assert.Equal(48, DisplayWidth.Of(lines[1]));
        Assert.Equal(48, DisplayWidth.Of(lines[2]));

        // 数字右对齐：金额列末尾是数字
        Assert.EndsWith("64.00", lines[1]);
        Assert.EndsWith("8.00", lines[2]);
        // 中文商品名左对齐
        Assert.StartsWith("宫保鸡丁", lines[1]);
    }

    [Fact]
    public void Paper58_LineWidth_Is_32()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm58,
            CutAfter = false,
            Elements = new List<TemplateElement> { new LineElement() },
        };
        string preview = new TemplateRenderer(template).RenderPreview();
        Assert.Equal(new string('-', 32), preview.Trim());
    }

    [Fact]
    public void Paper80_LineWidth_Is_48()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm80,
            CutAfter = false,
            Elements = new List<TemplateElement> { new LineElement() },
        };
        string preview = new TemplateRenderer(template).RenderPreview();
        Assert.Equal(new string('-', 48), preview.Trim());
    }

    [Fact]
    public void QrCode_Emits_GS_Parenthesis_k_Sequence()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm80,
            CutAfter = false,
            Elements = new List<TemplateElement>
            {
                new QrCodeElement { Content = "https://yihua/pay/{orderNo}", Size = 6 },
            },
        };
        byte[] bytes = new TemplateRenderer(template).Render(SampleVars());
        // GS ( k 出现在字节流中
        bool found = false;
        for (int i = 0; i + 2 < bytes.Length; i++)
            if (bytes[i] == 0x1D && bytes[i + 1] == 0x28 && bytes[i + 2] == 0x6B) { found = true; break; }
        Assert.True(found);
        string gb = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        Assert.Contains("https://yihua/pay/A001", gb); // 变量已替换
    }

    [Fact]
    public void Barcode_Emits_GS_k_Sequence()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm80,
            CutAfter = false,
            Elements = new List<TemplateElement>
            {
                new BarcodeElement { Content = "{orderNo}", Type = BarcodeType.Code128 },
            },
        };
        byte[] bytes = new TemplateRenderer(template).Render(SampleVars());
        // GS k m (m=73 Code128)
        bool found = false;
        for (int i = 0; i + 2 < bytes.Length; i++)
            if (bytes[i] == 0x1D && bytes[i + 1] == 0x6B && bytes[i + 2] == 73) { found = true; break; }
        Assert.True(found);
    }

    [Fact]
    public void CutAfter_Emits_Cut_Command()
    {
        var template = TextTemplate();
        template.CutAfter = true;
        byte[] bytes = new TemplateRenderer(template).Render(SampleVars());
        // GS V 0 全切
        bool found = false;
        for (int i = 0; i + 2 < bytes.Length; i++)
            if (bytes[i] == 0x1D && bytes[i + 1] == 0x56 && bytes[i + 2] == 0x00) { found = true; break; }
        Assert.True(found);
    }

    [Fact]
    public void Empty_QrCode_Content_Skipped()
    {
        var template = new PrintTemplate
        {
            Name = "t",
            Paper = PaperWidth.Mm80,
            CutAfter = false,
            Elements = new List<TemplateElement>
            {
                new QrCodeElement { Content = "" },
                new TextElement { Text = "hi" },
            },
        };
        byte[] bytes = new TemplateRenderer(template).Render(new Dictionary<string, object?>());
        string gb = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        Assert.Contains("hi", gb);
    }
}
