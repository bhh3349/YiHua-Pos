// MUSE-2 · T-105 电子秤协议测试
// 大华 OS2X/PS1X：15 字节定长帧，帧头 01 02，帧尾 03 04

using Xunit;
using YiHua.Pos.Hardware.Scales;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Tests.Hardware.Scales;

public class DaHuaProtocolTests
{
    private readonly DaHuaOs2xProtocol _protocol = new();

    // 正常帧："  1,250" kg，稳定
    // [01 02][S][  1,250][kg][保留][03 04]
    private static byte[] StableKgFrame() => new byte[]
    {
        0x01, 0x02, (byte)'S',
        0x20, 0x20, (byte)'1', (byte)',', (byte)'2', (byte)'5', (byte)'0',
        (byte)'k', (byte)'g',
        0x20,
        0x03, 0x04,
    };

    [Fact]
    public void TryParse_正常稳定帧_返回重量kg()
    {
        bool ok = _protocol.TryParse(StableKgFrame(), out decimal weight);

        Assert.True(ok);
        Assert.Equal(1.250m, weight);
    }

    [Fact]
    public void TryParseFrame_正常帧_状态为稳定()
    {
        bool ok = _protocol.TryParseFrame(StableKgFrame(), out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(ScaleStatus.Stable, status);
        Assert.Equal(1.250m, weight);
    }

    [Fact]
    public void TryParse_单位为克_换算成千克()
    {
        // 重量 "1250,00"，单位 "g " → 1250.00g = 1.25kg
        var frame = new byte[]
        {
            0x01, 0x02, (byte)'S',
            (byte)'1', (byte)'2', (byte)'5', (byte)'0', (byte)',', (byte)'0', (byte)'0',
            (byte)'g', 0x20,
            0x20,
            0x03, 0x04,
        };

        bool ok = _protocol.TryParse(frame, out decimal weight);

        Assert.True(ok);
        Assert.Equal(1.25m, weight);
    }

    [Fact]
    public void TryParse_半帧_丢弃返回false()
    {
        var half = new byte[] { 0x01, 0x02, (byte)'S', 0x20, 0x20, (byte)'1', (byte)',' };

        Assert.False(_protocol.TryParse(half, out _));
        Assert.False(_protocol.TryParseFrame(half, out _, out _));
    }

    [Fact]
    public void FindFrameStart_半帧_返回负一等待更多数据()
    {
        var half = new byte[] { 0x01, 0x02, (byte)'S', 0x20, 0x20, (byte)'1', (byte)',' };

        Assert.Equal(-1, _protocol.FindFrameStart(half));
    }

    [Fact]
    public void FindFrameStart_粘包两帧_定位到最后一帧()
    {
        var frame1 = StableKgFrame();
        var frame2 = StableKgFrame();
        var sticky = new byte[30];
        frame1.CopyTo(sticky, 0);
        frame2.CopyTo(sticky, 15);

        int start = _protocol.FindFrameStart(sticky);

        Assert.Equal(15, start);
        Assert.True(_protocol.TryParse(sticky.AsSpan(start, 15), out decimal weight));
        Assert.Equal(1.250m, weight);
    }

    [Fact]
    public void FindFrameStart_帧前有噪声_跳过噪声定位帧首()
    {
        var noise = new byte[] { 0xFF, 0x00, 0xAA, 0x55 };
        var frame = StableKgFrame();
        var buffer = new byte[noise.Length + frame.Length];
        noise.CopyTo(buffer, 0);
        frame.CopyTo(buffer, noise.Length);

        int start = _protocol.FindFrameStart(buffer);

        Assert.Equal(noise.Length, start);
    }

    [Fact]
    public void TryParse_帧头错误_返回false()
    {
        var frame = StableKgFrame();
        frame[0] = 0x00;
        frame[1] = 0x00;

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void TryParse_帧尾错误_返回false()
    {
        var frame = StableKgFrame();
        frame[13] = 0x00;
        frame[14] = 0x00;

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void TryParse_晃动状态_返回重量但状态为Weighting()
    {
        var frame = StableKgFrame();
        frame[2] = (byte)'U';

        bool ok = _protocol.TryParseFrame(frame, out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(1.250m, weight);
        Assert.Equal(ScaleStatus.Weighting, status);
        // 接口 TryParse：晃动时仍返回重量（稳定性由 IsStableAsync 暴露）
        Assert.True(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void TryParse_超载_返回false状态为OverLoad()
    {
        var frame = StableKgFrame();
        frame[2] = (byte)'F';

        bool richOk = _protocol.TryParseFrame(frame, out _, out ScaleStatus status);
        bool ok = _protocol.TryParse(frame, out _);

        Assert.True(richOk);
        Assert.Equal(ScaleStatus.OverLoad, status);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_零重量_状态为空秤()
    {
        var frame = StableKgFrame();
        // 重量字段写 "  0,000"
        var zero = new byte[] { 0x20, 0x20, (byte)'0', (byte)',', (byte)'0', (byte)'0', (byte)'0' };
        zero.CopyTo(frame, 3);

        bool ok = _protocol.TryParseFrame(frame, out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(0m, weight);
        Assert.Equal(ScaleStatus.UnWeight, status);
    }

    [Fact]
    public void BuildZeroCommand_返回置零指令()
    {
        Assert.Equal(
            new byte[] { 0x3C, 0x5A, 0x4B, 0x3E, 0x09 },
            _protocol.BuildZeroCommand());
    }

    [Fact]
    public void BuildTareCommand_返回去皮指令()
    {
        Assert.Equal(
            new byte[] { 0x3C, 0x54, 0x4B, 0x3E, 0x09 },
            _protocol.BuildTareCommand());
    }

    [Fact]
    public void BuildTakeWeightCommand_返回取重指令()
    {
        Assert.Equal(new byte[] { 0x11 }, _protocol.BuildTakeWeightCommand());
    }

    [Fact]
    public void Ps1x_协议名正确_帧格式与Os2x一致()
    {
        var ps1x = new DaHuaPs1xProtocol();

        Assert.Equal("DaHua.PS1X", ps1x.ProtocolName);
        Assert.Equal("DaHua.OS2X", _protocol.ProtocolName);
        Assert.True(ps1x.TryParse(StableKgFrame(), out decimal weight));
        Assert.Equal(1.250m, weight);
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
