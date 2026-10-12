using System.Text;
using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 测试打印服务（设置界面用）。
///
/// 为什么必须有：商家装好软件、配好打印机之后，
/// 需要一个「立刻能打出一张纸」的手段来确认打印机调好了。
/// 没有它，商家就只能打电话问客服「我这样配对不对」——
/// 每次至少 20 分钟，这是 299 定价下最贵的售后成本之一。
///
/// IReceiptPrinter 本身已有 TestAsync，这里负责：
///   ① 把失败原因翻译成商家听得懂的话
///   ② 生成可断言的测试页内容
/// </summary>
public interface ITestPrintService
{
    /// <summary>用指定打印机打印测试页。失败时返回人话原因。</summary>
    Task<(bool Ok, string? Error)> PrintTestPageAsync(
        IReceiptPrinter printer, int paperWidthMm, CancellationToken ct = default);
}

public sealed class TestPrintService : ITestPrintService
{
    public async Task<(bool Ok, string? Error)> PrintTestPageAsync(
        IReceiptPrinter printer, int paperWidthMm, CancellationToken ct = default)
    {
        if (printer is null)
            return (false, "请先选择打印机");

        try
        {
            bool connected = await printer.ConnectAsync(ct).ConfigureAwait(false);
            if (!connected)
                return (false, $"无法连接到打印机「{printer.Name}」。请检查线缆是否插好，或换一个 USB 口试试");

            await printer.PrintAsync(TestPrintContent.Build(paperWidthMm), ct).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 商家不懂设备路径，报原始异常等于没说
            return (false, $"打印失败：{ex.Message}。请确认打印机已开机、没有缺纸");
        }
    }
}

/// <summary>测试页内容生成（纯逻辑，便于逐行断言）。</summary>
public static class TestPrintContent
{
    /// <summary>系统版本串</summary>
    public const string Version = "YiHua POS · 打印测试页";

    /// <summary>
    /// 生成测试页。58mm = 32 列，80mm = 48 列（中文按 2 列宽计）。
    /// </summary>
    public static byte[] Build(int paperWidthMm)
    {
        int cols = ColumnsFor(paperWidthMm);
        var sb = new StringBuilder();

        sb.AppendLine(Center("逸华餐饮 打印测试", cols));
        sb.AppendLine(Center("打印机配置成功", cols));
        sb.AppendLine();
        sb.AppendLine(new string('-', cols));
        sb.AppendLine($"纸张宽度    {paperWidthMm}mm");
        sb.AppendLine($"可用列数    {cols}");
        sb.AppendLine($"打印时间{DateTime.Now:MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine(new string('-', cols));
        // 中文宽度测试：数字与汉字宽度不同，排版错乱一眼就能看出来
        sb.AppendLine("中文对齐测试（汉字占两列）");
        sb.AppendLine("ABCDEFG abcdefg 0123456789");
        sb.AppendLine("金额：¥128.00  ¥0.99  ¥1234.56");
        sb.AppendLine();
        sb.AppendLine("如打印正常，可关闭此页开始使用。");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>58mm = 32 列，80mm = 48 列。</summary>
    public static int ColumnsFor(int paperWidthMm) => paperWidthMm switch
    {
        <= 58 => 32,
        _ => 48,
    };

    /// <summary>居中（中文按 2 列宽）</summary>
    public static string Center(string text, int cols)
    {
        int w = DisplayWidth(text);
        if (w >= cols) return text;
        return new string(' ', (cols - w) / 2) + text;
    }

    /// <summary>显示宽度：CJK 记 2 列，ASCII 记 1 列。</summary>
    public static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int w = 0;
        foreach (char c in text)
            w += IsWide(c) ? 2 : 1;
        return w;
    }

    /// <summary>是否宽字符（CJK）</summary>
    public static bool IsWide(char c) =>
        (c >= 0x1100 && c <= 0x115F) ||
        (c >= 0x2E80 && c <= 0xA4CF) ||
        (c >= 0xAC00 && c <= 0xD7A3) ||
        (c >= 0xF900 && c <= 0xFAFF) ||
        (c >= 0xFE30 && c <= 0xFE6F) ||
        (c >= 0xFF00 && c <= 0xFF60) ||
        (c >= 0xFFE0 && c <= 0xFFE6);
}