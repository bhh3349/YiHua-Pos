using YiHua.Pos.Hardware.Printing.EscPos;
using YiHua.Pos.Hardware.Printing.Templates.Elements;

namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>内置默认模板：前台小票 / 后厨单 / 标签 / 交班单</summary>
public static class TemplateDefaults
{
    // ---------- 前台小票 ----------

    /// <summary>
    /// 前台小票：门店信息 / 桌号 / 商品明细表 / 合计 / 支付 / 找零 / 二维码。
    /// 变量：storeName, storePhone, storeAddress, orderNo, deskName, createdAt,
    /// items, amountTotal, amountDiscount, amountPay, amountReceived, amountChange,
    /// payTypeName, operatorName, remark, qrContent。
    /// </summary>
    public static PrintTemplate Receipt(PaperWidth paper = PaperWidth.Mm80)
    {
        int[] itemWidths = paper == PaperWidth.Mm58
            ? new[] { 16, 6, 10 }
            : new[] { 28, 8, 12 };

        return new PrintTemplate
        {
            Name = "前台小票",
            Paper = paper,
            Elements = new List<TemplateElement>
            {
                new TextElement { Text = "{storeName}", Align = AlignStyle.Center, Bold = true, FontWidth = 2, FontHeight = 2 },
                new TextElement { Text = "电话:{storePhone}", Align = AlignStyle.Center },
                new TextElement { Text = "{storeAddress}", Align = AlignStyle.Center },
                new LineElement(),
                new TextElement { Text = "订单号:{orderNo}  桌号:{deskName}" },
                new TextElement { Text = "下单时间:{createdAt}" },
                new LineElement(),
                new TableElement
                {
                    Columns = new[] { "商品", "数量", "金额" },
                    Widths = itemWidths,
                    Fields = new[] { "Name", "Qty", "Amount" },
                    DataSource = "items",
                },
                new LineElement(),
                new TextElement { Text = "合计:{amountTotal}", Align = AlignStyle.Right },
                new TextElement { Text = "折扣:{amountDiscount}", Align = AlignStyle.Right },
                new TextElement { Text = "应付:{amountPay}", Align = AlignStyle.Right, Bold = true },
                new TextElement { Text = "实收:{amountReceived}  找零:{amountChange}", Align = AlignStyle.Right },
                new TextElement { Text = "支付方式:{payTypeName}  收银员:{operatorName}" },
                new TextElement { Text = "备注:{remark}" },
                new LineElement(),
                new QrCodeElement { Content = "{qrContent}", Align = AlignStyle.Center, Size = 6 },
                new TextElement { Text = "谢谢惠顾，欢迎下次光临", Align = AlignStyle.Center },
                new SpacerElement { Lines = 1 },
            },
        };
    }

    // ---------- 后厨单 ----------

    /// <summary>
    /// 后厨单：桌号 / 时间 / 菜品 + 数量 + 备注（不放金额）。
    /// 变量：deskName, takeNo, createdAt, items, remark。
    /// </summary>
    public static PrintTemplate Kitchen(PaperWidth paper = PaperWidth.Mm80)
    {
        int[] itemWidths = paper == PaperWidth.Mm58
            ? new[] { 14, 6, 12 }
            : new[] { 24, 8, 16 };

        return new PrintTemplate
        {
            Name = "后厨单",
            Paper = paper,
            CutAfter = false,
            Elements = new List<TemplateElement>
            {
                new TextElement { Text = "{deskName}", Align = AlignStyle.Center, Bold = true, FontWidth = 2, FontHeight = 2 },
                new TextElement { Text = "取餐号:{takeNo}  时间:{createdAt}", Align = AlignStyle.Center },
                new LineElement { Char = '=' },
                new TableElement
                {
                    Columns = new[] { "菜品", "数量", "备注" },
                    Widths = itemWidths,
                    Fields = new[] { "Name", "Qty", "Remark" },
                    DataSource = "items",
                },
                new LineElement { Char = '=' },
                new TextElement { Text = "备注:{remark}", Bold = true },
                new SpacerElement { Lines = 2 },
            },
        };
    }

    // ---------- 标签 ----------

    /// <summary>
    /// 茶饮杯贴：取餐号（大字）/ 商品名 / 规格 / 时间。
    /// 变量：takeNo, itemName, spec, createdAt。
    /// </summary>
    public static PrintTemplate Label(PaperWidth paper = PaperWidth.Mm58)
    {
        return new PrintTemplate
        {
            Name = "标签",
            Paper = paper,
            Elements = new List<TemplateElement>
            {
                new TextElement { Text = "{takeNo}", Align = AlignStyle.Center, Bold = true, FontWidth = 3, FontHeight = 3 },
                new LineElement(),
                new TextElement { Text = "{itemName}", Align = AlignStyle.Center, Bold = true },
                new TextElement { Text = "规格:{spec}", Align = AlignStyle.Center },
                new TextElement { Text = "{createdAt}", Align = AlignStyle.Center },
            },
        };
    }

    // ---------- 交班单 ----------

    /// <summary>
    /// 交班单：班次时间 / 订单数 / 各支付方式金额 / 现金核对。
    /// 变量：shiftName, shiftBegin, shiftEnd, orderCount, payTypes(行: 方式/笔数/金额),
    /// cashExpected, cashActual, cashDiff, operatorName, createdAt。
    /// </summary>
    public static PrintTemplate Shift(PaperWidth paper = PaperWidth.Mm80)
    {
        int[] payWidths = paper == PaperWidth.Mm58
            ? new[] { 12, 8, 12 }
            : new[] { 20, 10, 18 };

        return new PrintTemplate
        {
            Name = "交班单",
            Paper = paper,
            Elements = new List<TemplateElement>
            {
                new TextElement { Text = "交班单", Align = AlignStyle.Center, Bold = true, FontWidth = 2, FontHeight = 2 },
                new TextElement { Text = "班次:{shiftName}", Align = AlignStyle.Center },
                new TextElement { Text = "{shiftBegin} ~ {shiftEnd}", Align = AlignStyle.Center },
                new LineElement(),
                new TextElement { Text = "订单数:{orderCount}" },
                new TableElement
                {
                    Columns = new[] { "支付方式", "笔数", "金额" },
                    Widths = payWidths,
                    DataSource = "payTypes",
                },
                new LineElement(),
                new TextElement { Text = "应有现金:{cashExpected}", Align = AlignStyle.Right },
                new TextElement { Text = "实点现金:{cashActual}", Align = AlignStyle.Right, Bold = true },
                new TextElement { Text = "差异:{cashDiff}", Align = AlignStyle.Right },
                new LineElement(),
                new TextElement { Text = "交班人:{operatorName}  时间:{createdAt}" },
                new SpacerElement { Lines = 1 },
            },
        };
    }
}
