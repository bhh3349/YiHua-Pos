// 仓鼠2号 · T-105 电子秤协议
// 托利多 ArivaS 串口协议（定长 31 字节）。
// 帧格式（依据《协议规范-电子秤.md》3.2）：
//   [0]      帧头 02 (STX)
//   [4]      稳定标志：0x30('0')=稳定，其他=不稳定
//   [6..10]  重量 ASCII（5 字节）；特殊值 "uuuuu"=错误，"^^^^^"=超载
//   [11..29] 其余字段（皮重/单价/金额，本实现暂不使用）
//   [30]     帧尾 03 (ETX)
// 单位固定为克，解析后 ÷1000 换算成 kg，保留 3 位小数。

namespace YiHua.Pos.Hardware.Scales.Protocols;

/// <summary>托利多 ArivaS 电子秤协议</summary>
public class ToledoArivaProtocol : ScaleProtocolBase
{
    public override string ProtocolName => "Toledo.ArivaS";
    public override int FrameLength => 31;

    // 指令表（3.1 节）：EOT(04) + STX(02) + "1x" + ETX(03)
    private static readonly byte[] TakeWeightCmd = { 0x04, 0x02, 0x31, 0x33, 0x03 };
    private static readonly byte[] ZeroCmd = { 0x04, 0x02, 0x31, 0x31, 0x03 };
    private static readonly byte[] TareCmd = { 0x04, 0x02, 0x31, 0x32, 0x03 };

    public override byte[] BuildZeroCommand() => (byte[])ZeroCmd.Clone();
    public override byte[] BuildTareCommand() => (byte[])TareCmd.Clone();
    public override byte[] BuildTakeWeightCommand() => (byte[])TakeWeightCmd.Clone();

    /// <summary>从后往前找帧尾 03 (ETX)，往前推 31 字节得帧首，校验 [0]==02（3.3 节算法）</summary>
    public override int FindFrameStart(ReadOnlySpan<byte> buffer)
    {
        for (int i = buffer.Length - 1; i >= FrameLength - 1; i--)
        {
            if (buffer[i] == 0x03)
            {
                int start = i - (FrameLength - 1);
                if (buffer[start] == 0x02)
                    return start;
                // 帧头不匹配：这个帧尾是噪声，继续往前找
            }
        }
        return -1;
    }

    public override bool TryParseFrame(ReadOnlySpan<byte> frame, out decimal weightKg, out ScaleStatus status)
    {
        weightKg = 0;
        status = ScaleStatus.Error;

        if (frame.Length != FrameLength)
            return false;
        if (frame[0] != 0x02 || frame[30] != 0x03)
            return false;

        // [4] 稳定标志
        bool stable = frame[4] == 0x30;

        // [6..10] 重量
        string weightText = GetAscii(frame.Slice(6, 5));
        if (weightText == "uuuuu")
        {
            status = ScaleStatus.Error;
            return true; // 帧合法，秤报错误
        }
        if (weightText == "^^^^^")
        {
            status = ScaleStatus.OverLoad;
            return true; // 帧合法，秤超载
        }
        if (!TryParseAsciiDecimal(frame.Slice(6, 5), out decimal grams))
            return false;

        // 单位固定为克 → kg
        weightKg = ToKilograms(grams, "g");

        status = stable ? ScaleStatus.Stable : ScaleStatus.Weighting;
        status = RefineStatusByWeight(weightKg, status);
        return true;
    }
}
