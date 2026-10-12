using System.Text;

namespace YiHua.Pos.Hardware.Printing.EscPos;

/// <summary>
/// ESC/POS 指令生成器（Fluent API）。
/// 依据爱普生 ESC/POS 公开标准实现，只依赖 BCL，零第三方依赖。
/// 实例级构建器（非线程安全）；指令常量表为 static readonly。
/// </summary>
public class EscPosBuilder
{
    static EscPosBuilder()
    {
        // .NET Core 默认只带 UTF-8/16/32，需显式注册代码页提供程序才能用 GB18030。
        // System.Text.Encoding.CodePages 随共享框架发布，无需额外 NuGet 包。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly List<byte> _buffer = new();
    private Encoding _textEncoding = Encoding.GetEncoding("GB18030");

    /// <summary>初始化打印机（ESC @）</summary>
    public EscPosBuilder Init()
    {
        _buffer.AddRange(EscPosCommands.Init);
        return this;
    }

    /// <summary>对齐方式（ESC a n：0左 1中 2右）</summary>
    public EscPosBuilder Align(AlignStyle style)
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x61);
        _buffer.Add((byte)style);
        return this;
    }

    /// <summary>加粗开关（ESC E n）</summary>
    public EscPosBuilder Bold(bool on)
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x45);
        _buffer.Add(on ? (byte)0x01 : (byte)0x00);
        return this;
    }

    /// <summary>下划线开关（ESC - n）</summary>
    public EscPosBuilder Underline(bool on)
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x2D);
        _buffer.Add(on ? (byte)0x01 : (byte)0x00);
        return this;
    }

    /// <summary>
    /// 字号（GS ! n）：宽高各 1~8 倍，n 的高 4 位为宽倍数、低 4 位为高倍数。
    /// </summary>
    public EscPosBuilder FontSize(int width, int height)
    {
        if (width < 1 || width > 8)
            throw new ArgumentOutOfRangeException(nameof(width), "字宽倍数必须在 1~8 之间");
        if (height < 1 || height > 8)
            throw new ArgumentOutOfRangeException(nameof(height), "字高倍数必须在 1~8 之间");

        _buffer.Add(0x1D);
        _buffer.Add(0x21);
        _buffer.Add((byte)(((width - 1) << 4) | (height - 1)));
        return this;
    }

    /// <summary>
    /// 字符集：中文切 GB18030（发送 FS &amp; 进入中文模式），
    /// 西文切 ASCII（发送 FS . 退出中文模式）。
    /// </summary>
    public EscPosBuilder Charset(CharsetType type)
    {
        if (type == CharsetType.Gb18030)
        {
            _buffer.AddRange(EscPosCommands.ChineseModeOn);
            _textEncoding = Encoding.GetEncoding("GB18030");
        }
        else
        {
            _buffer.AddRange(EscPosCommands.ChineseModeOff);
            _textEncoding = Encoding.ASCII;
        }
        return this;
    }

    /// <summary>文本（按当前字符集编码，不换行）</summary>
    public EscPosBuilder Text(string text)
    {
        if (!string.IsNullOrEmpty(text))
            _buffer.AddRange(_textEncoding.GetBytes(text));
        return this;
    }

    /// <summary>文本 + 换行（LF）</summary>
    public EscPosBuilder TextLine(string text)
    {
        Text(text);
        _buffer.AddRange(EscPosCommands.LineFeed);
        return this;
    }

    /// <summary>分隔线（80mm 纸、默认字号下 48 列 '-' + 换行）</summary>
    public EscPosBuilder Line()
    {
        const int columns = 48; // 80mm 纸 FontA 每行 48 字符
        for (int i = 0; i < columns; i++)
            _buffer.Add((byte)'-');
        _buffer.AddRange(EscPosCommands.LineFeed);
        return this;
    }

    /// <summary>走纸 n 行（ESC d n），n 取值 0~255</summary>
    public EscPosBuilder Feed(int lines = 1)
    {
        if (lines < 0 || lines > 255)
            throw new ArgumentOutOfRangeException(nameof(lines), "走纸行数必须在 0~255 之间");

        _buffer.Add(0x1B);
        _buffer.Add(0x64);
        _buffer.Add((byte)lines);
        return this;
    }

    /// <summary>切纸（GS V m）</summary>
    public EscPosBuilder Cut(CutMode mode)
    {
        _buffer.Add(0x1D);
        _buffer.Add(0x56);
        _buffer.Add((byte)mode);
        return this;
    }

    /// <summary>开钱箱（ESC p m t1 t2，走打印机 RJ11 口；引脚 2，通电 25*2ms，断电 250*2ms）</summary>
    public EscPosBuilder OpenDrawer()
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x70);
        _buffer.Add(0x00);
        _buffer.Add(0x19);
        _buffer.Add(0xFA);
        return this;
    }

    /// <summary>
    /// 二维码（GS ( k）：Model 2、纠错等级 L；模块尺寸 1~16，默认 6。
    /// 数据按当前字符集编码。
    /// </summary>
    public EscPosBuilder QrCode(string data, int size = 6)
    {
        if (string.IsNullOrEmpty(data))
            throw new ArgumentException("二维码内容不能为空", nameof(data));
        if (size < 1 || size > 16)
            throw new ArgumentOutOfRangeException(nameof(size), "二维码模块尺寸必须在 1~16 之间");

        byte[] payload = _textEncoding.GetBytes(data);

        _buffer.AddRange(EscPosCommands.QrModel2);

        _buffer.Add(0x1D); _buffer.Add(0x28); _buffer.Add(0x6B); // 设置模块尺寸
        _buffer.Add(0x04); _buffer.Add(0x00);
        _buffer.Add(0x31); _buffer.Add(0x50); _buffer.Add(0x30);
        _buffer.Add((byte)size);

        _buffer.AddRange(EscPosCommands.QrErrorCorrectionL);

        int len = payload.Length + 3; // 存储数据
        _buffer.Add(0x1D); _buffer.Add(0x28); _buffer.Add(0x6B);
        _buffer.Add((byte)(len & 0xFF));
        _buffer.Add((byte)((len >> 8) & 0xFF));
        _buffer.Add(0x31); _buffer.Add(0x50); _buffer.Add(0x30);
        _buffer.AddRange(payload);

        _buffer.AddRange(EscPosCommands.QrPrint);
        return this;
    }

    /// <summary>
    /// 一维码（GS k m）：先设条码高 100 点、模块宽 3、HRI 字符在下方，
    /// 再发送条码数据（ASCII，NUL 结尾）。
    /// </summary>
    public EscPosBuilder Barcode(string data, BarcodeType type)
    {
        if (string.IsNullOrEmpty(data))
            throw new ArgumentException("条码内容不能为空", nameof(data));

        _buffer.AddRange(EscPosCommands.BarcodeHeight);
        _buffer.AddRange(EscPosCommands.BarcodeWidth);
        _buffer.AddRange(EscPosCommands.BarcodeHriBelow);
        _buffer.Add(0x1D);
        _buffer.Add(0x6B);
        _buffer.Add((byte)type);
        _buffer.AddRange(Encoding.ASCII.GetBytes(data));
        _buffer.Add(0x00);
        return this;
    }

    /// <summary>位图（logo）：PNG 转 1-bit 点阵（GS v 0），超宽时等比缩小到 maxWidth</summary>
    public EscPosBuilder Image(byte[] pngBytes, int maxWidth)
    {
        _buffer.AddRange(EscPosImageConverter.ConvertToRaster(pngBytes, maxWidth));
        return this;
    }

    /// <summary>输出最终字节流</summary>
    public byte[] Build() => _buffer.ToArray();

    /// <summary>字节流转十六进制字符串（调试用），如 "1B 40"</summary>
    public static string ToHexString(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }
        return sb.ToString();
    }
}
