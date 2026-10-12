namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>
/// 显示宽度计算：一个汉字（全角）算 2 列，ASCII（半角）算 1 列。
/// 用于表格对齐、截断等排版计算。
/// </summary>
public static class DisplayWidth
{
    /// <summary>字符串的显示列数</summary>
    public static int Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        int w = 0;
        foreach (char c in text)
            w += IsWide(c) ? 2 : 1;
        return w;
    }

    /// <summary>按显示列宽截断（不会把一个汉字切成半个）</summary>
    public static string Truncate(string? text, int columns)
    {
        if (string.IsNullOrEmpty(text) || columns <= 0)
            return string.Empty;
        int w = 0;
        int i = 0;
        while (i < text.Length)
        {
            int cw = IsWide(text[i]) ? 2 : 1;
            if (w + cw > columns)
                break;
            w += cw;
            i++;
        }
        return text.Substring(0, i);
    }

    /// <summary>按显示列宽左对齐补空格（超长则截断）</summary>
    public static string PadRight(string? text, int columns)
    {
        text ??= string.Empty;
        text = Truncate(text, columns);
        int pad = columns - Of(text);
        return pad > 0 ? text + new string(' ', pad) : text;
    }

    /// <summary>按显示列宽右对齐补空格（超长则截断，保留右侧）</summary>
    public static string PadLeft(string? text, int columns)
    {
        text ??= string.Empty;
        int w = Of(text);
        if (w > columns)
        {
            // 从右侧截取（金额优先保留右侧）
            int i = text.Length;
            int rw = 0;
            while (i > 0)
            {
                int cw = IsWide(text[i - 1]) ? 2 : 1;
                if (rw + cw > columns) break;
                rw += cw;
                i--;
            }
            text = text.Substring(i);
            w = rw;
        }
        int pad = columns - w;
        return pad > 0 ? new string(' ', pad) + text : text;
    }

    /// <summary>按显示列宽居中（超长则截断）</summary>
    public static string PadCenter(string? text, int columns)
    {
        text ??= string.Empty;
        text = Truncate(text, columns);
        int pad = columns - Of(text);
        int left = pad / 2;
        return new string(' ', left) + text + new string(' ', pad - left);
    }

    private static bool IsWide(char c)
    {
        // CJK 统一表意文字及扩展、兼容区、中日韩符号、全角形式
        return (c >= 0x1100 && c <= 0x115F) ||   // 谚文 Jamo
               (c >= 0x2E80 && c <= 0x4DBF) ||   // CJK 部首/扩展A
               (c >= 0x4E00 && c <= 0x9FFF) ||   // CJK 统一
               (c >= 0xA000 && c <= 0xA4CF) ||   // 彝文
               (c >= 0xAC00 && c <= 0xD7AF) ||   // 谚文音节
               (c >= 0xF900 && c <= 0xFAFF) ||   // CJK 兼容
               (c >= 0xFE30 && c <= 0xFE4F) ||   // CJK 兼容形式
               (c >= 0xFF00 && c <= 0xFFEF);     // 全角 ASCII/半角片假名
    }
}
