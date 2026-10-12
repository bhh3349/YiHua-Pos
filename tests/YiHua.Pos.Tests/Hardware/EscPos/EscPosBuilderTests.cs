using System.IO.Compression;
using System.Text;
using Xunit;
using YiHua.Pos.Hardware.Printing.EscPos;

namespace YiHua.Pos.Tests.Hardware.EscPos;

/// <summary>
/// EscPosBuilder 单元测试：每个指令生成的字节序列与 ESC/POS 公开标准逐字节断言。
/// </summary>
public class EscPosBuilderTests
{
    static EscPosBuilderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Init_Emits_EscAt()
    {
        var bytes = new EscPosBuilder().Init().Build();
        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes);
    }

    [Theory]
    [InlineData(AlignStyle.Left, 0x00)]
    [InlineData(AlignStyle.Center, 0x01)]
    [InlineData(AlignStyle.Right, 0x02)]
    public void Align_Emits_Esc_a_n(AlignStyle style, byte n)
    {
        var bytes = new EscPosBuilder().Align(style).Build();
        Assert.Equal(new byte[] { 0x1B, 0x61, n }, bytes);
    }

    [Theory]
    [InlineData(true, 0x01)]
    [InlineData(false, 0x00)]
    public void Bold_Emits_Esc_E_n(bool on, byte n)
    {
        var bytes = new EscPosBuilder().Bold(on).Build();
        Assert.Equal(new byte[] { 0x1B, 0x45, n }, bytes);
    }

    [Theory]
    [InlineData(true, 0x01)]
    [InlineData(false, 0x00)]
    public void Underline_Emits_Esc_Dash_n(bool on, byte n)
    {
        var bytes = new EscPosBuilder().Underline(on).Build();
        Assert.Equal(new byte[] { 0x1B, 0x2D, n }, bytes);
    }

    [Theory]
    [InlineData(1, 1, 0x00)]
    [InlineData(2, 2, 0x11)]
    [InlineData(2, 1, 0x10)]
    [InlineData(1, 3, 0x02)]
    public void FontSize_Emits_Gs_21_n(int width, int height, byte n)
    {
        var bytes = new EscPosBuilder().FontSize(width, height).Build();
        Assert.Equal(new byte[] { 0x1D, 0x21, n }, bytes);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    public void FontSize_OutOfRange_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EscPosBuilder().FontSize(width, height));
    }

    [Fact]
    public void Charset_Gb18030_Emits_Fs_And()
    {
        var bytes = new EscPosBuilder().Charset(CharsetType.Gb18030).Build();
        Assert.Equal(new byte[] { 0x1C, 0x26 }, bytes);
    }

    [Fact]
    public void Charset_Ascii_Emits_Fs_Dot()
    {
        var bytes = new EscPosBuilder().Charset(CharsetType.Ascii).Build();
        Assert.Equal(new byte[] { 0x1C, 0x2E }, bytes);
    }

    [Fact]
    public void Text_Chinese_Uses_Gb18030_Encoding()
    {
        // "中文" 的 GB18030 编码固定为 D6 D0 CE C4（与 GBK 相同码位）
        var bytes = new EscPosBuilder().Text("中文").Build();
        Assert.Equal(new byte[] { 0xD6, 0xD0, 0xCE, 0xC4 }, bytes);
    }

    [Fact]
    public void Text_Ascii_Passthrough()
    {
        var bytes = new EscPosBuilder().Text("ABC123").Build();
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x31, 0x32, 0x33 }, bytes);
    }

    [Fact]
    public void TextLine_Appends_LF()
    {
        var bytes = new EscPosBuilder().TextLine("AB").Build();
        Assert.Equal(new byte[] { 0x41, 0x42, 0x0A }, bytes);
    }

    [Fact]
    public void Line_Emits_48_Dashes_Plus_LF()
    {
        var bytes = new EscPosBuilder().Line().Build();
        Assert.Equal(49, bytes.Length);
        Assert.All(bytes[..48], b => Assert.Equal((byte)'-', b));
        Assert.Equal(0x0A, bytes[48]);
    }

    [Theory]
    [InlineData(1, 0x01)]
    [InlineData(3, 0x03)]
    public void Feed_Emits_Esc_d_n(int lines, byte n)
    {
        var bytes = new EscPosBuilder().Feed(lines).Build();
        Assert.Equal(new byte[] { 0x1B, 0x64, n }, bytes);
    }

    [Theory]
    [InlineData(CutMode.Full, 0x00)]
    [InlineData(CutMode.Partial, 0x01)]
    public void Cut_Emits_Gs_V_m(CutMode mode, byte m)
    {
        var bytes = new EscPosBuilder().Cut(mode).Build();
        Assert.Equal(new byte[] { 0x1D, 0x56, m }, bytes);
    }

    [Fact]
    public void OpenDrawer_Emits_Esc_p_Sequence()
    {
        var bytes = new EscPosBuilder().OpenDrawer().Build();
        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, bytes);
    }

    [Fact]
    public void QrCode_Emits_Full_GS_Paren_k_Sequence()
    {
        var bytes = new EscPosBuilder().QrCode("Hi", 6).Build();
        var expected = new byte[]
        {
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00,       // Model 2
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x50, 0x30, 0x06,       // 模块尺寸 6
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30,             // 纠错等级 L
            0x1D, 0x28, 0x6B, 0x05, 0x00, 0x31, 0x50, 0x30, 0x48, 0x69, // 存储 "Hi"
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30,             // 打印
        };
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void QrCode_InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EscPosBuilder().QrCode("Hi", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EscPosBuilder().QrCode("Hi", 17));
    }

    [Fact]
    public void Barcode_Ean13_Emits_Full_Sequence()
    {
        var bytes = new EscPosBuilder().Barcode("690123456789", BarcodeType.Ean13).Build();

        var expected = new byte[] { 0x1D, 0x68, 0x64 }        // 条码高 100
            .Concat(new byte[] { 0x1D, 0x77, 0x03 })          // 模块宽 3
            .Concat(new byte[] { 0x1D, 0x48, 0x02 })          // HRI 在下方
            .Concat(new byte[] { 0x1D, 0x6B, 0x02 })          // GS k EAN13
            .Concat(Encoding.ASCII.GetBytes("690123456789"))  // 数据
            .Concat(new byte[] { 0x00 })                      // NUL 结尾
            .ToArray();

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Fluent_Chaining_Accumulates_In_Order()
    {
        var bytes = new EscPosBuilder()
            .Init()
            .Align(AlignStyle.Center)
            .TextLine("TEST")
            .Feed()
            .Cut(CutMode.Full)
            .Build();

        var expected = new byte[]
        {
            0x1B, 0x40,             // Init
            0x1B, 0x61, 0x01,       // 居中
            0x54, 0x45, 0x53, 0x54, 0x0A, // "TEST\n"
            0x1B, 0x64, 0x01,       // 走纸 1
            0x1D, 0x56, 0x00,       // 全切
        };
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void ToHexString_Formats_Uppercase_SpaceSeparated()
    {
        Assert.Equal("1B 40", EscPosBuilder.ToHexString(new byte[] { 0x1B, 0x40 }));
        Assert.Equal(string.Empty, EscPosBuilder.ToHexString(Array.Empty<byte>()));
    }

    [Fact]
    public void Image_8x8_Checkerboard_Produces_Correct_Raster()
    {
        byte[] png = BuildTestPng(8, 8, (x, y) => (x + y) % 2 == 0);

        byte[] cmd = EscPosImageConverter.ConvertToRaster(png, 384);

        // GS v 0 头：xL=1（8px→1 字节/行），yL=8；数据 8 字节；首行 0xAA
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x08, 0x00 }, cmd[..8]);
        Assert.Equal(16, cmd.Length);
        Assert.Equal(0xAA, cmd[8]);
    }

    [Fact]
    public void Image_Scales_Down_When_Wider_Than_MaxWidth()
    {
        byte[] png = BuildTestPng(100, 10, (x, y) => true); // 全黑 100x10

        byte[] cmd = EscPosImageConverter.ConvertToRaster(png, 50);

        // 缩到 50x5：xL=7（50bit→7 字节/行），yL=5
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x07, 0x00, 0x05, 0x00 }, cmd[..8]);
        Assert.Equal(8 + 7 * 5, cmd.Length);
    }

    [Fact]
    public void Image_Builder_Integration_Appends_Raster_Command()
    {
        byte[] png = BuildTestPng(8, 8, (x, y) => true);

        var bytes = new EscPosBuilder().Init().Image(png, 384).Build();

        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes[..2]); // Init
        Assert.Equal(0x1D, bytes[2]);                        // GS v 0 紧随其后
        Assert.Equal(0x76, bytes[3]);
    }

    // ---------- 测试用最小 PNG 编码器（8 位 RGB，非隔行；解码器不校验 CRC） ----------

    private static byte[] BuildTestPng(int width, int height, Func<int, int, bool> isBlack)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        using (var ihdr = new MemoryStream())
        {
            WriteInt32BE(ihdr, width);
            WriteInt32BE(ihdr, height);
            ihdr.WriteByte(8); // 位深
            ihdr.WriteByte(2); // 真彩 RGB
            ihdr.WriteByte(0); // 压缩方法
            ihdr.WriteByte(0); // 过滤方法
            ihdr.WriteByte(0); // 非隔行
            WriteChunk(ms, "IHDR", ihdr.ToArray());
        }

        using (var raw = new MemoryStream())
        {
            for (int y = 0; y < height; y++)
            {
                raw.WriteByte(0); // 过滤器 None
                for (int x = 0; x < width; x++)
                {
                    byte v = isBlack(x, y) ? (byte)0 : (byte)255;
                    raw.WriteByte(v);
                    raw.WriteByte(v);
                    raw.WriteByte(v);
                }
            }
            WriteChunk(ms, "IDAT", ZlibWrap(raw.ToArray()));
        }

        WriteChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, string type, byte[] data)
    {
        WriteInt32BE(ms, data.Length);
        ms.Write(Encoding.ASCII.GetBytes(type));
        ms.Write(data);
        WriteInt32BE(ms, 0); // CRC：解码器不校验，填 0
    }

    private static byte[] ZlibWrap(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw, 0, raw.Length);
        uint adler = Adler32(raw);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static void WriteInt32BE(Stream s, int v)
    {
        s.WriteByte((byte)(v >> 24));
        s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }
}
