using System.Text;

namespace YiHua.Pos.UI.Services.Report;

/// <summary>
/// CSV 写入器：UTF-8 BOM（Excel 打开中文不乱码）+ 中文表头。
/// </summary>
public static class CsvWriter
{
    /// <summary>
    /// 写入 CSV 文件（带 BOM）。
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="headers">中文表头</param>
    /// <param name="rows">数据行（每行与表头列数一致）</param>
    public static async Task WriteAsync(string path, string[] headers, IEnumerable<string[]> rows)
    {
        if (headers == null || headers.Length == 0)
            throw new ArgumentException("表头不能为空", nameof(headers));

        // UTF-8 BOM：Excel 识别中文的关键
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(JoinRow(headers));
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                throw new ArgumentException($"数据列数 ({row.Length}) 与表头 ({headers.Length}) 不一致");
            await writer.WriteLineAsync(JoinRow(row));
        }
    }

    /// <summary>
    /// 写入到字符串（用于测试）。
    /// </summary>
    public static string WriteToString(string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        // BOM 字符
        sb.Append('\uFEFF');
        sb.AppendLine(JoinRow(headers));
        foreach (var row in rows)
            sb.AppendLine(JoinRow(row));
        return sb.ToString();
    }

    private static string JoinRow(string[] fields)
        => string.Join(",", fields.Select(Escape));

    /// <summary>
    /// 转义：含逗号/引号/换行时加引号，引号双写。
    /// </summary>
    public static string Escape(string field)
    {
        if (field == null) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }
}

/// <summary>
/// 报表导出器：导出当前筛选条件的数据。
/// </summary>
public class ReportExporter
{
    /// <summary>
    /// 导出菜品排行 CSV。
    /// </summary>
    public static Task ExportProductRankAsync(string path, List<RankItem> items)
    {
        var headers = new[] { "排名", "菜品", "销量", "销售额", "占比" };
        var rows = items.Select(r => new[]
        {
            r.Rank.ToString(),
            r.Name,
            r.Quantity.ToString("F0"),
            r.Amount.ToString("F2"),
            (r.Share * 100).ToString("F1") + "%",
        });
        return CsvWriter.WriteAsync(path, headers, rows);
    }

    /// <summary>
    /// 导出订单明细 CSV。
    /// </summary>
    public static Task ExportOrdersAsync(string path, List<ReportOrderDto> orders)
    {
        var headers = new[] { "订单号", "桌台", "下单时间", "结账时间", "商品总额", "优惠", "应收", "实收", "找零", "状态" };
        var rows = orders.Where(o => !o.IsVoid).Select(o => new[]
        {
            o.OrderNo,
            o.DeskName,
            o.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            o.SettleTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
            o.AmountTotal.ToString("F2"),
            o.AmountDiscount.ToString("F2"),
            o.AmountPay.ToString("F2"),
            o.AmountReceived.ToString("F2"),
            o.AmountChange.ToString("F2"),
            GetStatusName(o.Status),
        });
        return CsvWriter.WriteAsync(path, headers, rows);
    }

    private static string GetStatusName(int status) => status switch
    {
        0 => "待结账",
        1 => "已结账",
        2 => "已退单",
        3 => "作废",
        _ => "未知",
    };
}
