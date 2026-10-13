using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Printing.EscPos;
using YiHua.Pos.UI.Services.Payment;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.UI.Services.Printing;

// IReceiptPrinter 在 Payment 和 Hardware.Abstractions 各有一个（前者是结账页的窄接口），
// 这里要的是硬件层那个，显式取别名避免 CS0104。
using HardwarePrinter = YiHua.Pos.Hardware.Abstractions.IReceiptPrinter;

/// <summary>按配置创建打印机实例（App 层注入 Windows 实现；测试注入假实现）。</summary>
public interface IPrinterFactory
{
    HardwarePrinter Create(PrinterConfigDto config);
}

/// <summary>
/// 小票打印服务。
///
/// 一条原则：**打不出来必须让收银员知道**。
/// 小票打失败还一声不吭，客人拿不到凭证、后厨拿不到单，
/// 等发现的时候已经过了好几桌。
/// </summary>
public class ReceiptPrintService
{
    private readonly ISettingsStore _store;
    private readonly IPrinterFactory _factory;

    public ReceiptPrintService(ISettingsStore store, IPrinterFactory factory)
    {
        _store = store;
        _factory = factory;
    }

    /// <summary>取配置里启用的小票机（没有就返回 null）。</summary>
    public async Task<PrinterConfigDto?> GetReceiptPrinterAsync(CancellationToken ct = default)
    {
        var printers = await _store.GetPrintersAsync(ct);
        return printers.FirstOrDefault(p => p.Enabled && p.Usage == PrinterUsage.Receipt);
    }

    /// <summary>
    /// 结账后打印小票。
    /// 返回 (是否成功, 给收银员看的说明)。
    /// </summary>
    public async Task<(bool Ok, string Message)> PrintReceiptAsync(ReceiptData data, CancellationToken ct = default)
    {
        var config = await GetReceiptPrinterAsync(ct);
        if (config == null)
            return (false, "没有配置小票机，未打印小票。到「设置 → 打印机管理」加一台。");

        try
        {
            var printer = _factory.Create(config);
            if (!await printer.ConnectAsync(ct))
                return (false, $"小票机「{config.Name}」连不上，未打印。请检查电源和连接。");

            var bytes = BuildReceipt(data, config.PaperWidth);
            await printer.PrintAsync(bytes, ct);

            // 现金收款顺手开钱箱
            if (data.PayMethodName == "现金")
            {
                try { await printer.OpenCashDrawerAsync(ct); }
                catch { /* 钱箱开不了不该影响小票已打成功这个事实 */ }
            }

            return (true, "小票已打印");
        }
        catch (Exception ex)
        {
            return (false, $"小票打印失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 生成小票 ESC/POS 字节（可测试的纯逻辑）。
    /// 版式按 58mm=32 列 / 80mm=48 列排，中文按 2 列宽计算。
    /// </summary>
    public static byte[] BuildReceipt(ReceiptData data, int paperWidthMm)
    {
        var cols = ColumnsFor(paperWidthMm);

        var builder = new EscPosBuilder();
        builder.Init()
               .Charset(CharsetType.Gb18030)
               .Align(AlignStyle.Center)
               .Bold(true)
               .FontSize(1, 1)
               .TextLine("逸华餐饮")
               .Bold(false)
               .Align(AlignStyle.Left);

        foreach (var line in BuildReceiptLines(data, cols))
            builder.TextLine(line);

        builder.Feed(1)
               .Align(AlignStyle.Center)
               .TextLine("谢谢光临，欢迎再来")
               .Feed(3)
               .Cut(CutMode.Full);

        return builder.Build();
    }

    /// <summary>纸宽 → 列数。</summary>
    public static int ColumnsFor(int paperWidthMm) => paperWidthMm switch
    {
        <= 58 => 32,
        _ => 48,
    };

    /// <summary>
    /// 小票正文的纯文本行（不含 ESC/POS 指令）。
    ///
    /// 把排版和编码拆开是为了能测：字节流里混着指令，
    /// 想验证「这行没超宽」就得先把指令剥掉，剥不干净就会误判。
    /// </summary>
    public static IReadOnlyList<string> BuildReceiptLines(ReceiptData data, int cols)
    {
        var lines = new List<string>
        {
            new string('-', cols),
            $"单号：{data.OrderNo}",
        };

        if (!string.IsNullOrWhiteSpace(data.DeskInfo))
            lines.Add($"桌台：{data.DeskInfo}");

        lines.Add($"时间：{data.PayTime:yyyy-MM-dd HH:mm:ss}");
        lines.Add(new string('-', cols));

        foreach (var item in data.Items)
        {
            // 商品名单独一行：名称长的时候和数量挤在一起会看不清点的什么
            var name = item.Name;
            if (!string.IsNullOrWhiteSpace(item.SpecText))
                name += $"（{item.SpecText}）";
            lines.Add(TruncateToWidth(name, cols));

            var left = $"  {FormatQty(item.Quantity)} × {item.Price:F2}";
            var right = $"¥{item.Amount:F2}";
            lines.Add(PadLine(left, right, cols));
        }

        lines.Add(new string('-', cols));
        lines.Add(PadLine("原价", $"¥{data.TotalAmount:F2}", cols));

        if (data.DiscountAmount > 0)
        {
            var label = string.IsNullOrWhiteSpace(data.DiscountLabel) ? "优惠" : $"优惠（{data.DiscountLabel}）";
            lines.Add(PadLine(label, $"-¥{data.DiscountAmount:F2}", cols));
        }

        lines.Add(PadLine("应收", $"¥{data.PayableAmount:F2}", cols));
        lines.Add(PadLine(data.PayMethodName, $"¥{data.ReceivedAmount:F2}", cols));

        if (data.ChangeAmount > 0)
            lines.Add(PadLine("找零", $"¥{data.ChangeAmount:F2}", cols));

        return lines;
    }

    /// <summary>数量文本：整数不显示小数，称重保留两位。</summary>
    public static string FormatQty(decimal qty)
        => qty == Math.Floor(qty) ? qty.ToString("0") : qty.ToString("0.00");

    /// <summary>
    /// 左右对齐成一行。
    /// 中文按 2 列算，否则「桌台」这种两字词会把右边数字挤歪。
    /// </summary>
    public static string PadLine(string left, string right, int cols)
    {
        var leftWidth = DisplayWidth(left);
        var rightWidth = DisplayWidth(right);
        var spaces = cols - leftWidth - rightWidth;

        if (spaces < 1)
        {
            // 挤不下：左半截断，保证右边的金额完整可见
            // —— 金额看不见的小票等于没打
            var keep = Math.Max(0, cols - rightWidth - 1);
            left = TruncateToWidth(left, keep);
            spaces = Math.Max(1, cols - DisplayWidth(left) - rightWidth);
        }

        return left + new string(' ', spaces) + right;
    }

    /// <summary>显示宽度：CJK 全角算 2 列，ASCII 算 1 列。</summary>
    public static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var width = 0;
        foreach (var ch in text)
            width += IsWide(ch) ? 2 : 1;
        return width;
    }

    /// <summary>是否全角（CJK 区段）。</summary>
    public static bool IsWide(char c)
        => (c >= 0x1100 && c <= 0x115F) ||
           (c >= 0x2E80 && c <= 0xA4CF) ||
           (c >= 0xAC00 && c <= 0xD7A3) ||
           (c >= 0xF900 && c <= 0xFAFF) ||
           (c >= 0xFE30 && c <= 0xFE6F) ||
           (c >= 0xFF00 && c <= 0xFF60) ||
           (c >= 0xFFE0 && c <= 0xFFE6);

    /// <summary>按显示宽度截断（不切半个汉字）。</summary>
    public static string TruncateToWidth(string text, int maxWidth)
    {
        var sb = new System.Text.StringBuilder();
        var width = 0;
        foreach (var ch in text)
        {
            var w = IsWide(ch) ? 2 : 1;
            if (width + w > maxWidth) break;
            sb.Append(ch);
            width += w;
        }
        return sb.ToString();
    }
}