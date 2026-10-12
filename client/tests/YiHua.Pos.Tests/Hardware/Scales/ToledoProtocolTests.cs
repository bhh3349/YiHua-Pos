// 仓鼠2号 · T-105 电子秤协议测试
// 托利多 ArivaS：31 字节定长帧，帧头 02 (STX)，帧尾 03 (ETX)

using Xunit;
using YiHua.Pos.Hardware.Scales;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Tests.Hardware.Scales;

public class ToledoProtocolTests
{
    private readonly ToledoArivaProtocol _protocol = new();

    // 正常帧：稳定(0x30)，重量 "01250"（克）
    // [02][___][0x30][_]["01250"][19 字节其他字段][03]
    private static byte[] StableFrame()
    {
        var frame = new byte[31];
        frame[0] = 0x02;
        frame[1] = 0x20; frame[2] = 0x20; frame[3] = 0x20;
        frame[4] = 0x30; // '0' = 稳定
        frame[5] = 0x20;
        frame[6] = (byte)'0'; frame[7] = (byte)'1'; frame[8] = (byte)'2';
        frame[9] = (byte)'5'; frame[10] = (byte)'0';
        for (int i = 11; i < 30; i++) frame[i] = 0x20;
        frame[30] = 0x03;
        return frame;
    }

    [Fact]
    public void TryParse_正常稳定帧_克换算成千克()
    {
        bool ok = _protocol.TryParse(StableFrame(), out decimal weight);

        Assert.True(ok);
        Assert.Equal(1.25m, weight); // 1250g = 1.25kg
    }

    [Fact]
    public void TryParseFrame_正常帧_状态为稳定()
    {
        bool ok = _protocol.TryParseFrame(StableFrame(), out _, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(ScaleStatus.Stable, status);
    }

    [Fact]
    public void TryParseFrame_不稳定标志_状态为Weighting但仍返回重量()
    {
        var frame = StableFrame();
        frame[4] = 0x31; // 非 '0' = 不稳定

        bool ok = _protocol.TryParseFrame(frame, out decimal weight, out ScaleStatus status);

        Assert.True(ok);
        Assert.Equal(1.25m, weight);
        Assert.Equal(ScaleStatus.Weighting, status);
    }

    [Fact]
    public void TryParse_重量为uuuuu_秤报错误()
    {
        var frame = StableFrame();
        for (int i = 6; i <= 10; i++) frame[i] = (byte)'u';

        bool richOk = _protocol.TryParseFrame(frame, out _, out ScaleStatus status);
        bool ok = _protocol.TryParse(frame, out _);

        Assert.True(richOk);
        Assert.Equal(ScaleStatus.Error, status);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_重量为五个尖号_超载()
    {
        var frame = StableFrame();
        for (int i = 6; i <= 10; i++) frame[i] = (byte)'^';

        bool richOk = _protocol.TryParseFrame(frame, out _, out ScaleStatus status);
        bool ok = _protocol.TryParse(frame, out _);

        Assert.True(richOk);
        Assert.Equal(ScaleStatus.OverLoad, status);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_半帧_丢弃返回false()
    {
        var half = new byte[20];
        Array.Copy(StableFrame(), half, 20);

        Assert.False(_protocol.TryParse(half, out _));
        Assert.Equal(-1, _protocol.FindFrameStart(half));
    }

    [Fact]
    public void FindFrameStart_粘包两帧_定位到最后一帧()
    {
        var frame1 = StableFrame();
        var frame2 = StableFrame();
        var sticky = new byte[62];
        frame1.CopyTo(sticky, 0);
        frame2.CopyTo(sticky, 31);

        int start = _protocol.FindFrameStart(sticky);

        Assert.Equal(31, start);
        Assert.True(_protocol.TryParse(sticky.AsSpan(start, 31), out decimal weight));
        Assert.Equal(1.25m, weight);
    }

    [Fact]
    public void TryParse_帧头错误_返回false()
    {
        var frame = StableFrame();
        frame[0] = 0x00;

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void TryParse_帧尾错误_返回false()
    {
        var frame = StableFrame();
        frame[30] = 0x00;

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void TryParse_重量字段非数字_返回false()
    {
        var frame = StableFrame();
        frame[6] = (byte)'A'; frame[7] = (byte)'B'; frame[8] = (byte)'C';
        frame[9] = (byte)'D'; frame[10] = (byte)'E';

        Assert.False(_protocol.TryParse(frame, out _));
    }

    [Fact]
    public void BuildZeroCommand_返回置零指令()
    {
        Assert.Equal(
            new byte[] { 0x04, 0x02, 0x31, 0x31, 0x03 },
            _protocol.BuildZeroCommand());
    }

    [Fact]
    public void BuildTareCommand_返回去皮指令()
    {
        Assert.Equal(
            new byte[] { 0x04, 0x02, 0x31, 0x32, 0x03 },
            _protocol.BuildTareCommand());
    }

    [Fact]
    public void BuildTakeWeightCommand_返回取重指令()
    {
        Assert.Equal(
            new byte[] { 0x04, 0x02, 0x31, 0x33, 0x03 },
            _protocol.BuildTakeWeightCommand());
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
