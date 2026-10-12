// MUSE-2 · T-105 电子秤协议测试
// 凯士 CAS216：12 字节定长帧，无帧头帧尾，被动接收
// [0..7] 重量 ASCII（8 字节，空格左填充） [8..9] 单位 [10..11] 保留

using Xunit;
using YiHua.Pos.Hardware.Scales;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Tests.Hardware.Scales;

public class KssProtocolTests
{
    private readonly KssCas216Protocol _protocol = new();

    // 正常帧："   1.250" + "kg" + CRLF
    private static byte[] KgFrame() => new byte[]
    {
        0x20, 0x20, 0x20, (byte)'1', (byte)'.', (byte)'2', (byte)'5', (byte)'0',
        (byte)'k', (byte)'g',
        0x0D, 0x0A,
    };

    [Fact]
    public void TryParse_正常帧_返回重量kg()
    {
        bool ok = _protocol.TryParse(KgFrame(), out decimal weight);

        Assert.True(ok);
        Assert.Equal(1.250m, weight);
    }

    [Fact]
    public void TryParseFrame_正常帧_状态为稳定()
    {
        // CAS216 无稳定标志位：推送即当前值，视为稳定
        bool ok = _protocol.TryParseFrame(KgFrame(), out _, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(ScaleStatus.Stable, status);
    }

    [Fact]
    public void TryParse_单位为克_换算成千克()
    {
        // " 1250.00" + "g "
        var frame = new byte[]
        {
            0x20, (byte)'1', (byte)'2', (byte)'5', (byte)'0', (byte)'.', (byte)'0', (byte)'0',
            (byte)'g', 0x20,
            0x0D, 0x0A,
        };

        bool ok = _protocol.TryParse(frame, out decimal weight);

        Assert.True(ok);
        Assert.Equal(1.25m, weight); // 1250.00g = 1.25kg
    }

    [Fact]
    public void TryParse_半帧_丢弃返回false()
    {
        var half = new byte[] { 0x20, 0x20, 0x20, (byte)'1', (byte)'.', (byte)'2', (byte)'5', (byte)'0' };

        Assert.False(_protocol.TryParse(half, out _));
    }

    [Fact]
    public void FindFrameStart_粘包两帧_定位到第一帧()
    {
        var frame1 = KgFrame();
        var frame2 = KgFrame();
        var sticky = new byte[24];
        frame1.CopyTo(sticky, 0);
        frame2.CopyTo(sticky, 12);

        // 无帧头帧尾：滑动窗口找到第一个合法帧
        int start = _protocol.FindFrameStart(sticky);

        Assert.Equal(0, start);
        Assert.True(_protocol.TryParse(sticky.AsSpan(start, 12), out decimal weight));
        Assert.Equal(1.250m, weight);
    }

    [Fact]
    public void FindFrameStart_帧前有噪声_跳过噪声()
    {
        var noise = new byte[] { 0xFF, 0x00, 0xAA };
        var frame = KgFrame();
        var buffer = new byte[noise.Length + frame.Length];
        noise.CopyTo(buffer, 0);
        frame.CopyTo(buffer, noise.Length);

        int start = _protocol.FindFrameStart(buffer);

        Assert.Equal(noise.Length, start);
    }

    [Fact]
    public void FindFrameStart_纯噪声_返回负一()
    {
        var noise = new byte[] { 0xFF, 0x00, 0xAA, 0x55, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };

        Assert.Equal(-1, _protocol.FindFrameStart(noise));
    }

    [Fact]
    public void TryParse_零重量_状态为空秤()
    {
        var frame = new byte[]
        {
            0x20, 0x20, 0x20, (byte)'0', (byte)'.', (byte)'0', (byte)'0', (byte)'0',
            (byte)'k', (byte)'g',
            0x0D, 0x0A,
        };

        bool ok = _protocol.TryParseFrame(frame, out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(0m, weight);
        Assert.Equal(ScaleStatus.UnWeight, status);
    }

    [Fact]
    public void TryParse_负重量_状态为负重量()
    {
        var frame = new byte[]
        {
            0x20, 0x20, (byte)'-', (byte)'0', (byte)'.', (byte)'5', (byte)'0', (byte)'0',
            (byte)'k', (byte)'g',
            0x0D, 0x0A,
        };

        bool ok = _protocol.TryParseFrame(frame, out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(-0.5m, weight);
        Assert.Equal(ScaleStatus.NegativeWeight, status);
    }

    [Fact]
    public void TryParse_未知单位_返回false()
    {
        var frame = KgFrame();
        frame[8] = (byte)'l'; frame[9] = (byte)'b'; // "lb" 不在规范内

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void BuildZeroCommand_不支持_返回空()
    {
        // CAS216 被动接收，不支持置零
        Assert.Empty(_protocol.BuildZeroCommand());
    }

    [Fact]
    public void BuildTareCommand_不支持_返回空()
    {
        Assert.Empty(_protocol.BuildTareCommand());
    }

    [Fact]
    public void 串口参数_9600_8N1()
    {
        Assert.Equal(9600, _protocol.BaudRate);
        Assert.Equal(8, _protocol.DataBits);
        Assert.Equal("None", _protocol.Parity);
        Assert.Equal("One", _protocol.StopBits);
    }
}
