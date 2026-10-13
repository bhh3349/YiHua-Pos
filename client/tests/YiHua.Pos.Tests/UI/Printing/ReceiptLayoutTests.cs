using YiHua.Pos.UI.Services.Payment;
using YiHua.Pos.UI.Services.Printing;
using Xunit;

namespace YiHua.Pos.Tests.UI.Printing;

/// <summary>
/// 小票排版测试。
///
/// 小票是给客人看的凭证，排版错了很显眼：
/// 金额被截断、中文把右边数字挤歪、58mm 机器上打出 48 列的字。
/// 这些都是纯计算，必须锁住。
/// </summary>
public class ReceiptLayoutTests
{
    [Fact]
    public void 中文算两列_ASCII算一列()
    {
        Assert.Equal(4, ReceiptPrintService.DisplayWidth("牛肉"));
        Assert.Equal(4, ReceiptPrintService.DisplayWidth("abcd"));
        Assert.Equal(6, ReceiptPrintService.DisplayWidth("牛肉面"));
        Assert.Equal(8, ReceiptPrintService.DisplayWidth("牛肉面ab"));
    }

    [Fact]
    public void 空串宽度为零()
    {
        Assert.Equal(0, ReceiptPrintService.DisplayWidth(null));
        Assert.Equal(0, ReceiptPrintService.DisplayWidth(string.Empty));
    }

    [Fact]
    public void 左右对齐_总宽等于列数()
    {
        var line = ReceiptPrintService.PadLine("原价", "¥18.00", 32);

        Assert.Equal(32, ReceiptPrintService.DisplayWidth(line));
        Assert.StartsWith("原价", line);
        Assert.EndsWith("¥18.00", line);
    }

    [Fact]
    public void 左右对齐_80mm用48列()
    {
        var line = ReceiptPrintService.PadLine("合计", "¥128.57", 48);

        Assert.Equal(48, ReceiptPrintService.DisplayWidth(line));
    }

    [Fact]
    public void 左边太长时_金额必须完整保留()
    {
        // 小票上最不能少的就是金额：被截断的金额等于没打
        var longName = "超级豪华双人套餐加赠饮料两杯不要葱不要香菜";
        var line = ReceiptPrintService.PadLine(longName, "¥288.00", 32);

        Assert.EndsWith("¥288.00", line);
        Assert.True(ReceiptPrintService.DisplayWidth(line) <= 32,
            $"行宽 {ReceiptPrintService.DisplayWidth(line)} 超过 32 列");
    }

    [Fact]
    public void 截断不切半个汉字()
    {
        var truncated = ReceiptPrintService.TruncateToWidth("牛肉面套餐", 5);

        // 5 列放得下「牛肉」4 列 + 1 列放不下第三个汉字，所以是「牛肉」
        Assert.Equal("牛肉", truncated);
        Assert.True(ReceiptPrintService.DisplayWidth(truncated) <= 5);
    }

    [Fact]
    public void 数量格式_整数不显示小数()
    {
        Assert.Equal("2", ReceiptPrintService.FormatQty(2m));
        Assert.Equal("1", ReceiptPrintService.FormatQty(1.0m));

        // 称重商品要保留两位，0.35 不能变成 0
        Assert.Equal("0.35", ReceiptPrintService.FormatQty(0.35m));
        Assert.Equal("1.25", ReceiptPrintService.FormatQty(1.25m));
    }

    [Fact]
    public void 小票正文包含单号桌台与各项金额()
    {
        var data = new ReceiptData
        {
            OrderNo = "L20261014-0001",
            DeskInfo = "A01",
            TotalAmount = 18.57m,
            DiscountAmount = 0.07m,
            DiscountLabel = "抹分",
            PayableAmount = 18.50m,
            ReceivedAmount = 20m,
            ChangeAmount = 1.50m,
            PayMethodName = "现金",
            PayTime = new DateTime(2026, 10, 14, 3, 0, 0),
        };
        data.Items.Add(new ReceiptItem
        {
            Name = "兰州牛肉面", SpecText = string.Empty,
            Quantity = 1m, Price = 18.57m, Amount = 18.57m,
        });

        var lines = ReceiptPrintService.BuildReceiptLines(data, 48);
        var text = string.Join("\n", lines);

        Assert.Contains("L20261014-0001", text);
        Assert.Contains("A01", text);
        Assert.Contains("兰州牛肉面", text);
        Assert.Contains("¥18.57", text);
        Assert.Contains("¥18.50", text);
        Assert.Contains("¥1.50", text);
        Assert.Contains("现金", text);
        Assert.Contains("抹分", text);
    }

    [Fact]
    public void 没有优惠时不打印优惠行()
    {
        var data = new ReceiptData { OrderNo = "T1", TotalAmount = 18m, PayableAmount = 18m };

        var text = string.Join("\n", ReceiptPrintService.BuildReceiptLines(data, 48));

        Assert.DoesNotContain("优惠", text);
    }

    [Fact]
    public void 没有找零时不打印找零行()
    {
        var data = new ReceiptData { OrderNo = "T1", PayableAmount = 18m, ReceivedAmount = 18m, ChangeAmount = 0m };

        var text = string.Join("\n", ReceiptPrintService.BuildReceiptLines(data, 48));

        Assert.DoesNotContain("找零", text);
    }

    [Fact]
    public void 小票字节以初始化指令开头()
    {
        var data = new ReceiptData { OrderNo = "T1", PayableAmount = 10m, ReceivedAmount = 10m };

        var bytes = ReceiptPrintService.BuildReceipt(data, 80);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x1B, bytes[0]);
    }

    [Fact]
    public void 小票以切纸指令结尾()
    {
        var data = new ReceiptData { OrderNo = "T1", PayableAmount = 10m, ReceivedAmount = 10m };

        var bytes = ReceiptPrintService.BuildReceipt(data, 58);

        // GS V 0 是切纸
        var tail = bytes.Skip(Math.Max(0, bytes.Length - 4)).ToArray();
        Assert.Contains((byte)0x1D, tail);
        Assert.Contains((byte)0x56, tail);
    }

    [Fact]
    public void 纸宽决定列数()
    {
        Assert.Equal(32, ReceiptPrintService.ColumnsFor(58));
        Assert.Equal(32, ReceiptPrintService.ColumnsFor(50));
        Assert.Equal(48, ReceiptPrintService.ColumnsFor(80));
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void 小票每一行都不超过纸宽(int paperWidthMm)
    {
        var cols = ReceiptPrintService.ColumnsFor(paperWidthMm);
        var data = new ReceiptData
        {
            OrderNo = "L20261014-0001",
            DeskInfo = "A01",
            TotalAmount = 128.57m,
            DiscountAmount = 0.07m,
            DiscountLabel = "抹分",
            PayableAmount = 128.50m,
            ReceivedAmount = 200m,
            ChangeAmount = 71.50m,
            PayMethodName = "现金",
        };
        data.Items.Add(new ReceiptItem
        {
            Name = "超级豪华双人套餐加赠饮料两杯", SpecText = "微辣不要葱",
            Quantity = 2m, Price = 64.285m, Amount = 128.57m,
        });

        var lines = ReceiptPrintService.BuildReceiptLines(data, cols);

        foreach (var line in lines)
        {
            var width = ReceiptPrintService.DisplayWidth(line);
            Assert.True(width <= cols, $"{paperWidthMm}mm 小票某行宽 {width} 列（上限 {cols}）：「{line}」");
        }
    }
}