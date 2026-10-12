using System.IO.Compression;

namespace YiHua.Pos.Hardware.Printing.EscPos;

/// <summary>
/// PNG 位图转 ESC/POS 点阵（GS v 0 m xL xH yL yH）。
/// 内置最小 PNG 解码器：只依赖 BCL（DeflateStream 解 IDAT），零第三方依赖。
/// 支持非隔行 PNG：8 位灰度/RGB/灰度+Alpha/RGBA，以及 1/2/4/8 位索引色；
/// 透明像素视为白色。超宽图片按 maxWidth 等比缩小（最近邻）。
/// </summary>
public static class EscPosImageConverter
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// PNG 字节 → GS v 0 点阵指令字节（含指令头）。
    /// 输出为 1-bit 黑白点阵（MSB 在左，黑点为 1），行字节数向上补齐到整字节。
    /// </summary>
    /// <exception cref="ArgumentException">pngBytes 为空或 maxWidth 非法</exception>
    /// <exception cref="InvalidDataException">PNG 格式不支持或已损坏</exception>
    public static byte[] ConvertToRaster(byte[] pngBytes, int maxWidth)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            throw new ArgumentException("PNG 数据不能为空", nameof(pngBytes));
        if (maxWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "最大宽度必须大于 0");

        var (width, height, rgba) = DecodePng(pngBytes);

        if (width > maxWidth)
        {
            double scale = (double)maxWidth / width;
            int newWidth = maxWidth;
            int newHeight = Math.Max(1, (int)Math.Round(height * scale));
            rgba = ScaleNearest(rgba, width, height, newWidth, newHeight);
            width = newWidth;
            height = newHeight;
        }

        int bytesPerRow = (width + 7) / 8;
        byte[] raster = new byte[bytesPerRow * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                byte r = rgba[i], g = rgba[i + 1], b = rgba[i + 2], a = rgba[i + 3];

                bool black = a >= 128 && (r * 299 + g * 587 + b * 114) < 128 * 1000;
                if (black)
                    raster[y * bytesPerRow + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        var cmd = new List<byte>(8 + raster.Length);
        cmd.Add(0x1D); cmd.Add(0x76); cmd.Add(0x30); cmd.Add(0x00); // GS v 0 m=0（普通点阵）
        cmd.Add((byte)(bytesPerRow & 0xFF));
        cmd.Add((byte)((bytesPerRow >> 8) & 0xFF));
        cmd.Add((byte)(height & 0xFF));
        cmd.Add((byte)((height >> 8) & 0xFF));
        cmd.AddRange(raster);
        return cmd.ToArray();
    }

    // ================= PNG 解码（最小实现） =================

    private static (int width, int height, byte[] rgba) DecodePng(byte[] png)
    {
        if (png.Length < 8)
            throw new InvalidDataException("PNG 数据过短");
        for (int i = 0; i < 8; i++)
        {
            if (png[i] != PngSignature[i])
                throw new InvalidDataException("PNG 签名错误");
        }

        int width = 0, height = 0, bitDepth = 0, colorType = -1;
        byte[]? palette = null;
        var idat = new List<byte>();

        int pos = 8;
        bool end = false;
        while (!end)
        {
            if (pos + 8 > png.Length)
                throw new InvalidDataException("PNG chunk 头越界");
            int length = ReadInt32BE(png, pos);
            if (length < 0 || pos + 12 + length > png.Length)
                throw new InvalidDataException("PNG chunk 数据越界");

            string type = $"{(char)png[pos + 4]}{(char)png[pos + 5]}{(char)png[pos + 6]}{(char)png[pos + 7]}";
            switch (type)
            {
                case "IHDR":
                    width = ReadInt32BE(png, pos + 8);
                    height = ReadInt32BE(png, pos + 12);
                    bitDepth = png[pos + 16];
                    colorType = png[pos + 17];
                    if (png[pos + 18] != 0 || png[pos + 19] != 0)
                        throw new InvalidDataException("不支持的 PNG 压缩/过滤方法");
                    if (png[pos + 20] != 0)
                        throw new InvalidDataException("不支持隔行扫描 PNG（Adam7）");
                    break;
                case "PLTE":
                    palette = new byte[length];
                    Array.Copy(png, pos + 8, palette, 0, length);
                    break;
                case "IDAT":
                    for (int i = 0; i < length; i++)
                        idat.Add(png[pos + 8 + i]);
                    break;
                case "IEND":
                    end = true;
                    break;
            }
            pos += 12 + length;
        }

        if (width <= 0 || height <= 0 || colorType < 0)
            throw new InvalidDataException("PNG 缺少有效 IHDR");
        if (idat.Count == 0)
            throw new InvalidDataException("PNG 缺少 IDAT");

        byte[] raw = ZlibDecompress(idat.ToArray());
        return UnfilterToRgba(width, height, bitDepth, colorType, palette, raw);
    }

    /// <summary>RFC1950 zlib：2 字节头 + deflate 流 + 4 字节 Adler32</summary>
    private static byte[] ZlibDecompress(byte[] zlibData)
    {
        if (zlibData.Length < 6)
            throw new InvalidDataException("zlib 数据过短");
        if ((zlibData[0] & 0x0F) != 8)
            throw new InvalidDataException("zlib 压缩方法不是 deflate");
        if (((zlibData[0] << 8) | zlibData[1]) % 31 != 0)
            throw new InvalidDataException("zlib 头校验失败");

        using var input = new MemoryStream(zlibData, 2, zlibData.Length - 6, false);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static (int width, int height, byte[] rgba) UnfilterToRgba(
        int width, int height, int bitDepth, int colorType, byte[]? palette, byte[] raw)
    {
        int channels = colorType switch
        {
            0 => 1, // 灰度
            2 => 3, // 真彩 RGB
            3 => 1, // 索引色
            4 => 2, // 灰度 + Alpha
            6 => 4, // 真彩 + Alpha
            _ => throw new InvalidDataException($"不支持的 PNG 颜色类型 {colorType}"),
        };

        if (bitDepth != 8 && bitDepth != 4 && bitDepth != 2 && bitDepth != 1)
            throw new InvalidDataException($"不支持的 PNG 位深 {bitDepth}");
        if (bitDepth != 8 && colorType != 0 && colorType != 3)
            throw new InvalidDataException("非 8 位深只支持灰度与索引色");

        // 过滤器按"像素字节数"向前引用；亚字节像素按整字节算
        int filterBpp = Math.Max(1, channels * bitDepth / 8);
        int stride = (width * channels * bitDepth + 7) / 8; // 每行数据字节数（不含过滤器字节）

        if (raw.Length < height * (1 + stride))
            throw new InvalidDataException("PNG 图像数据不完整");

        byte[] rgba = new byte[width * height * 4];
        byte[] prev = new byte[stride];
        byte[] cur = new byte[stride];

        int src = 0;
        for (int y = 0; y < height; y++)
        {
            int filterType = raw[src++];
            Array.Copy(raw, src, cur, 0, stride);
            src += stride;
            UnfilterRow(filterType, cur, prev, filterBpp);

            for (int x = 0; x < width; x++)
            {
                byte r, g, b, a = 255;
                if (bitDepth == 8)
                {
                    int o = x * channels;
                    switch (colorType)
                    {
                        case 0:
                            r = g = b = cur[o];
                            break;
                        case 2:
                            r = cur[o]; g = cur[o + 1]; b = cur[o + 2];
                            break;
                        case 3:
                            (r, g, b) = PaletteColor(palette, cur[o]);
                            break;
                        case 4:
                            r = g = b = cur[o]; a = cur[o + 1];
                            break;
                        default: // 6
                            r = cur[o]; g = cur[o + 1]; b = cur[o + 2]; a = cur[o + 3];
                            break;
                    }
                }
                else
                {
                    // 1/2/4 位：MSB 优先打包
                    int bitPos = x * bitDepth;
                    int val = (cur[bitPos / 8] >> (8 - bitDepth - (bitPos % 8))) & ((1 << bitDepth) - 1);
                    if (colorType == 0)
                    {
                        r = g = b = (byte)(val * 255 / ((1 << bitDepth) - 1));
                    }
                    else
                    {
                        (r, g, b) = PaletteColor(palette, (byte)val);
                    }
                }

                int dst = (y * width + x) * 4;
                rgba[dst] = r;
                rgba[dst + 1] = g;
                rgba[dst + 2] = b;
                rgba[dst + 3] = a;
            }

            var tmp = prev;
            prev = cur;
            cur = tmp;
        }

        return (width, height, rgba);
    }

    private static (byte r, byte g, byte b) PaletteColor(byte[]? palette, byte index)
    {
        int o = index * 3;
        if (palette == null || o + 2 >= palette.Length)
            throw new InvalidDataException("PLTE 缺失或调色板索引越界");
        return (palette[o], palette[o + 1], palette[o + 2]);
    }

    private static void UnfilterRow(int filterType, byte[] cur, byte[] prev, int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                break;
            case 1: // Sub
                for (int i = bpp; i < cur.Length; i++)
                    cur[i] = (byte)(cur[i] + cur[i - bpp]);
                break;
            case 2: // Up
                for (int i = 0; i < cur.Length; i++)
                    cur[i] = (byte)(cur[i] + prev[i]);
                break;
            case 3: // Average
                for (int i = 0; i < cur.Length; i++)
                {
                    int a = i >= bpp ? cur[i - bpp] : 0;
                    cur[i] = (byte)(cur[i] + ((a + prev[i]) >> 1));
                }
                break;
            case 4: // Paeth
                for (int i = 0; i < cur.Length; i++)
                {
                    int a = i >= bpp ? cur[i - bpp] : 0;
                    int b = prev[i];
                    int c = i >= bpp ? prev[i - bpp] : 0;
                    cur[i] = (byte)(cur[i] + PaethPredictor(a, b, c));
                }
                break;
            default:
                throw new InvalidDataException($"不支持的 PNG 行过滤器 {filterType}");
        }
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
    }

    private static byte[] ScaleNearest(byte[] rgba, int srcW, int srcH, int dstW, int dstH)
    {
        byte[] dst = new byte[dstW * dstH * 4];
        for (int y = 0; y < dstH; y++)
        {
            int sy = Math.Min(srcH - 1, (int)(y * (double)srcH / dstH));
            for (int x = 0; x < dstW; x++)
            {
                int sx = Math.Min(srcW - 1, (int)(x * (double)srcW / dstW));
                Array.Copy(rgba, (sy * srcW + sx) * 4, dst, (y * dstW + x) * 4, 4);
            }
        }
        return dst;
    }

    private static int ReadInt32BE(byte[] buf, int pos) =>
        (buf[pos] << 24) | (buf[pos + 1] << 16) | (buf[pos + 2] << 8) | buf[pos + 3];
}
