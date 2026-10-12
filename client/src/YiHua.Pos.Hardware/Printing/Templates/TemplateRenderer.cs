using System.Collections;
using YiHua.Pos.Hardware.Printing.EscPos;
using YiHua.Pos.Hardware.Printing.Templates.Elements;

namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>
/// 模板渲染器：模板 + 变量上下文 → ESC/POS 字节流。
/// 基于 T-102 的 <see cref="EscPosBuilder"/>，零第三方依赖。
/// 同时提供 <see cref="RenderPreview"/> 输出纯文本排版（测试断言 / 小票预览用）。
/// </summary>
public sealed class TemplateRenderer
{
    private readonly PrintTemplate _template;

    public TemplateRenderer(PrintTemplate template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>渲染为 ESC/POS 字节流</summary>
    public byte[] Render(IReadOnlyDictionary<string, object?>? vars = null)
    {
        var b = new EscPosBuilder();
        b.Init();
        b.Charset(CharsetType.Gb18030);

        var ctx = vars ?? new Dictionary<string, object?>();
        foreach (var el in _template.Elements)
            RenderElement(b, el, ctx);

        if (_template.CutAfter)
        {
            b.Feed(3);
            b.Cut(CutMode.Full);
        }
        return b.Build();
    }

    /// <summary>渲染为纯文本预览（无控制码，用于测试断言与小票样张）</summary>
    public string RenderPreview(IReadOnlyDictionary<string, object?>? vars = null)
    {
        var ctx = vars ?? new Dictionary<string, object?>();
        var sb = new System.Text.StringBuilder();
        foreach (var el in _template.Elements)
            AppendPreview(sb, el, ctx);
        return sb.ToString();
    }

    public static byte[] RenderTemplate(PrintTemplate template, IReadOnlyDictionary<string, object?>? vars = null)
        => new TemplateRenderer(template).Render(vars);

    // ---------- 元素渲染 ----------

    private void RenderElement(EscPosBuilder b, TemplateElement el, IReadOnlyDictionary<string, object?> ctx)
    {
        int lineWidth = _template.Paper.CharsPerLine() / Math.Max(1, el.FontWidth);
        ApplyStyle(b, el);

        switch (el)
        {
            case TextElement t:
                string text = TemplateVariableResolver.Resolve(t.Text, ctx);
                b.TextLine(AlignLine(text, lineWidth, el.Align));
                break;
            case LineElement l:
                b.TextLine(new string(l.Char, lineWidth));
                break;
            case SpacerElement s:
                b.Feed(Math.Max(1, Math.Min(255, s.Lines)));
                break;
            case TableElement tb:
                foreach (string row in BuildTableRows(tb, ctx))
                    b.TextLine(row);
                break;
            case QrCodeElement q:
                string qc = TemplateVariableResolver.Resolve(q.Content, ctx);
                if (!string.IsNullOrEmpty(qc))
                {
                    b.Align(el.Align);
                    b.QrCode(qc, Math.Max(1, Math.Min(16, q.Size)));
                    b.Align(AlignStyle.Left);
                }
                break;
            case BarcodeElement bc:
                string bcc = TemplateVariableResolver.Resolve(bc.Content, ctx);
                if (!string.IsNullOrEmpty(bcc))
                {
                    b.Align(el.Align);
                    b.Barcode(ToAscii(bcc), bc.Type);
                    b.Align(AlignStyle.Left);
                }
                break;
            case ImageElement img:
                if (img.PngData != null && img.PngData.Length > 0)
                {
                    b.Align(el.Align);
                    b.Image(img.PngData, _template.Paper == PaperWidth.Mm58 ? 384 : 576);
                    b.Align(AlignStyle.Left);
                }
                break;
        }

        ResetStyle(b, el);
    }

    private static void ApplyStyle(EscPosBuilder b, TemplateElement el)
    {
        if (el.Bold) b.Bold(true);
        if (el.FontWidth != 1 || el.FontHeight != 1) b.FontSize(el.FontWidth, el.FontHeight);
    }

    private static void ResetStyle(EscPosBuilder b, TemplateElement el)
    {
        if (el.Bold) b.Bold(false);
        if (el.FontWidth != 1 || el.FontHeight != 1) b.FontSize(1, 1);
    }

    private static string AlignLine(string text, int lineWidth, AlignStyle align) =>
        align switch
        {
            AlignStyle.Center => DisplayWidth.PadCenter(text, lineWidth),
            AlignStyle.Right => DisplayWidth.PadLeft(text, lineWidth),
            _ => DisplayWidth.PadRight(text, lineWidth),
        };

    private static string ToAscii(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c <= 0x7E ? c : '?');
        return sb.ToString();
    }

    // ---------- 表格 ----------

    private List<string> BuildTableRows(TableElement tb, IReadOnlyDictionary<string, object?> ctx)
    {
        var rows = new List<string>();
        string[] cols = tb.Columns ?? Array.Empty<string>();
        int[] widths = tb.Widths ?? Array.Empty<int>();
        if (cols.Length == 0 || widths.Length == 0 || cols.Length != widths.Length)
            return rows;

        if (tb.ShowHeader)
            rows.Add(JoinCells(cols, widths));

        foreach (var cells in ResolveTableData(tb, ctx))
            rows.Add(JoinCells(cells, widths));

        return rows;
    }

    private static string JoinCells(IReadOnlyList<string> cells, int[] widths)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < widths.Length; i++)
        {
            string cell = i < cells.Count ? cells[i] ?? string.Empty : string.Empty;
            // 数字型单元格右对齐，其余左对齐
            sb.Append(IsNumericCell(cell)
                ? DisplayWidth.PadLeft(cell, widths[i])
                : DisplayWidth.PadRight(cell, widths[i]));
        }
        return sb.ToString();
    }

    private static bool IsNumericCell(string cell)
    {
        if (string.IsNullOrWhiteSpace(cell)) return false;
        return decimal.TryParse(cell.Trim(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private static IEnumerable<IReadOnlyList<string>> ResolveTableData(
        TableElement tb, IReadOnlyDictionary<string, object?> ctx)
    {
        if (string.IsNullOrEmpty(tb.DataSource))
            yield break;
        if (!ctx.TryGetValue(tb.DataSource, out object? raw) || raw == null)
            yield break;
        if (raw is string)
            yield break;

        string[] fields = (tb.Fields != null && tb.Fields.Length > 0)
            ? tb.Fields
            : new[] { "Name", "Qty", "Amount" };

        if (raw is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item is PrintItemRow r)
                    yield return MapRow(r, fields);
                else if (item is IReadOnlyList<string> cells)
                    yield return cells;
                else if (item is IEnumerable<string> strCells)
                    yield return new List<string>(strCells);
            }
        }
    }

    private static IReadOnlyList<string> MapRow(PrintItemRow row, string[] fields)
    {
        var cells = new List<string>(fields.Length);
        foreach (string f in fields)
        {
            cells.Add(f.ToLowerInvariant() switch
            {
                "name" => row.Name,
                "spec" => row.Spec,
                "qty" => TrimNumber(row.Qty),
                "price" => row.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                "amount" => row.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                "remark" => row.Remark,
                _ => string.Empty,
            });
        }
        return cells;
    }

    private static string TrimNumber(decimal d) =>
        d == Math.Truncate(d)
            ? ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : d.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // ---------- 纯文本预览 ----------

    private void AppendPreview(System.Text.StringBuilder sb, TemplateElement el, IReadOnlyDictionary<string, object?> ctx)
    {
        int lineWidth = _template.Paper.CharsPerLine();
        switch (el)
        {
            case TextElement t:
                sb.AppendLine(AlignLine(TemplateVariableResolver.Resolve(t.Text, ctx), lineWidth, el.Align).TrimEnd());
                break;
            case LineElement l:
                sb.AppendLine(new string(l.Char, lineWidth));
                break;
            case SpacerElement s:
                for (int i = 0; i < Math.Max(1, s.Lines); i++) sb.AppendLine();
                break;
            case TableElement tb:
                foreach (string row in BuildTableRows(tb, ctx))
                    sb.AppendLine(row.TrimEnd());
                break;
            case QrCodeElement q:
                sb.AppendLine($"[QR: {TemplateVariableResolver.Resolve(q.Content, ctx)}]");
                break;
            case BarcodeElement bc:
                sb.AppendLine($"[BARCODE: {TemplateVariableResolver.Resolve(bc.Content, ctx)}]");
                break;
            case ImageElement:
                sb.AppendLine("[IMAGE]");
                break;
        }
    }
}
