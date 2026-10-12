using System.Globalization;
using System.Text.RegularExpressions;

namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>
/// 模板变量替换器：把模板中的 {变量名} 替换为上下文中的值。
/// 未知变量原样保留；金额保留两位小数；时间按 yyyy-MM-dd HH:mm:ss 格式化。
/// </summary>
public static class TemplateVariableResolver
{
    private static readonly Regex VarPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>替换模板中的 {变量}</summary>
    public static string Resolve(string? template, IReadOnlyDictionary<string, object?>? vars)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;
        if (vars == null || vars.Count == 0)
            return template;

        return VarPattern.Replace(template, m =>
        {
            string name = m.Groups[1].Value;
            if (!vars.TryGetValue(name, out object? value) || value == null)
                return m.Value; // 未知变量原样保留
            return FormatValue(name, value);
        });
    }

    /// <summary>按变量语义格式化值</summary>
    public static string FormatValue(string name, object value)
    {
        // 金额类变量：两位小数
        if (IsAmountVar(name) && TryToDecimal(value, out decimal amount))
            return amount.ToString("F2", CultureInfo.InvariantCulture);

        // 时间类变量
        if (IsTimeVar(name) && TryToDateTime(value, out DateTime dt))
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        // 数字：Qty 等保持原样（去尾零由调用方决定，这里直接 ToString）
        if (value is IFormattable f)
            return f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    /// <summary>金额格式化（两位小数）</summary>
    public static string FormatAmount(object? value) =>
        TryToDecimal(value, out decimal d)
            ? d.ToString("F2", CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>时间格式化（yyyy-MM-dd HH:mm:ss）</summary>
    public static string FormatTime(object? value) =>
        TryToDateTime(value, out DateTime dt)
            ? dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : string.Empty;

    private static bool IsAmountVar(string name) =>
        name.StartsWith("amount", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("cash", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("total", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("price", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("change", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("received", StringComparison.OrdinalIgnoreCase);

    private static bool IsTimeVar(string name) =>
        name.EndsWith("At", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Time", StringComparison.OrdinalIgnoreCase);

    private static bool TryToDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value == null) return false;
        try { result = Convert.ToDecimal(value, CultureInfo.InvariantCulture); return true; }
        catch { return false; }
    }

    private static bool TryToDateTime(object? value, out DateTime result)
    {
        result = default;
        if (value == null) return false;
        if (value is DateTime dt) { result = dt; return true; }
        if (value is DateTimeOffset dto) { result = dto.DateTime; return true; }
        try { result = Convert.ToDateTime(value, CultureInfo.InvariantCulture); return true; }
        catch { return false; }
    }
}
