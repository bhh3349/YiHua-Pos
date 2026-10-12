using System.Text;

namespace YiHua.Pos.Hardware.Devices;

/// <summary>
/// VFD 客显文本编码策略。
/// 多数 VFD 只支持 ASCII；部分国产 VFD 支持 GB18030/GBK 中文。
/// </summary>
public enum VfdTextEncoding
{
    /// <summary>纯 ASCII，非 ASCII 字符降级为 '?'</summary>
    Ascii,

    /// <summary>GB18030，中文可直接显示</summary>
    Gb18030,
}

/// <summary>
/// VFD 客显指令构造器（ESC/POS 兼容）。
/// 典型规格 20 列 × 2 行。
/// </summary>
public sealed class VfdCommandBuilder
{
    private readonly List<byte> _buffer = new();
    private readonly Encoding _encoding;

    public VfdCommandBuilder(VfdTextEncoding encoding = VfdTextEncoding.Ascii)
    {
        _encoding = encoding == VfdTextEncoding.Gb18030
            ? Encoding.GetEncoding("GB18030")
            : Encoding.ASCII;
    }

    /// <summary>初始化（ESC @）</summary>
    public VfdCommandBuilder Init()
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x40);
        return this;
    }

    /// <summary>清屏（0C）</summary>
    public VfdCommandBuilder Clear()
    {
        _buffer.Add(0x0C);
        return this;
    }

    /// <summary>光标定位（ESC l 行 列），行列从 0 开始</summary>
    public VfdCommandBuilder SetCursor(int row, int col)
    {
        if (row < 0 || row > 255) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col > 255) throw new ArgumentOutOfRangeException(nameof(col));
        _buffer.Add(0x1B);
        _buffer.Add(0x6C);
        _buffer.Add((byte)row);
        _buffer.Add((byte)col);
        return this;
    }

    /// <summary>亮度（ESC * n），n 一般 1~4</summary>
    public VfdCommandBuilder Brightness(int level)
    {
        if (level < 0 || level > 255) throw new ArgumentOutOfRangeException(nameof(level));
        _buffer.Add(0x1B);
        _buffer.Add(0x2A);
        _buffer.Add((byte)level);
        return this;
    }

    /// <summary>写文本（自动做编码安全处理）</summary>
    public VfdCommandBuilder Text(string text)
    {
        if (!string.IsNullOrEmpty(text))
            _buffer.AddRange(_encoding.GetBytes(Sanitize(text)));
        return this;
    }

    public byte[] Build() => _buffer.ToArray();

    /// <summary>
    /// 编码安全处理：ASCII 模式下，非 ASCII 可打印字符降级为 '?'；
    /// 控制字符（除 \n \r \t）一律去掉。
    /// </summary>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r' || c == '\t')
                continue;
            if (c < 0x20 || c == 0x7F)
                continue;
            sb.Append(c <= 0x7E ? c : '?');
        }
        return sb.ToString();
    }

    /// <summary>按显示列宽截断（ASCII 模式 1 字符=1 列）</summary>
    public static string TruncateByColumns(string text, int columns)
    {
        string safe = Sanitize(text);
        return safe.Length <= columns ? safe : safe.Substring(0, columns);
    }
}
