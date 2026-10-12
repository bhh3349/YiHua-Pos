// 仓鼠2号 · T-105 电子秤协议
// 大华 Aclas OS2X 串口协议（定长 15 字节）。
// 帧格式（依据《协议规范-电子秤.md》2.2）：
//   [0..1]   帧头 01 02
//   [2]      状态字符 'S'=稳定 'U'=晃动 'F'=超载
//   [3..9]   重量 ASCII（7 字节，空格填充，逗号作小数点），例 "  1,250"
//   [10..11] 单位 ASCII（"kg"/"g"）
//   [12]     保留
//   [13..14] 帧尾 03 04

namespace YiHua.Pos.Hardware.Scales.Protocols;

/// <summary>大华 OS2X 电子秤协议</summary>
public class DaHuaOs2xProtocol : ScaleProtocolBase
{
    public override string ProtocolName => "DaHua.OS2X";
    public override int FrameLength => 15;

    // 指令表（2.1 节）
    private static readonly byte[] InitCmd = { 0x3C, 0x50, 0x42, 0x3E, 0x09 }; // <PB>\t
    private static readonly byte[] TakeWeightCmd = { 0x11 };
    private static readonly byte[] ZeroCmd = { 0x3C, 0x5A, 0x4B, 0x3E, 0x09 }; // <ZK>\t
    private static readonly byte[] TareCmd = { 0x3C, 0x54, 0x4B, 0x3E, 0x09 }; // <TK>\t

    public override byte[] BuildZeroCommand() => (byte[])ZeroCmd.Clone();
    public override byte[] BuildTareCommand() => (byte[])TareCmd.Clone();
    public override byte[] BuildTakeWeightCommand() => (byte[])TakeWeightCmd.Clone();
    public override byte[]? BuildInitCommand() => (byte[])InitCmd.Clone();

    /// <summary>从后往前找帧尾 03 04，往前推 15 字节得帧首（2.3 节算法）</summary>
    public override int FindFrameStart(ReadOnlySpan<byte> buffer)
    {
        for (int i = buffer.Length - 2; i >= 0; i--)
        {
            if (buffer[i] == 0x03 && buffer[i + 1] == 0x04)
            {
                int start = i - 13;
                if (start < 0)
                    return -1; // 帧尾找到了但前面不足 15 字节：半包，等更多数据
                if (buffer[start] == 0x01 && buffer[start + 1] == 0x02)
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
        if (frame[0] != 0x01 || frame[1] != 0x02)
            return false;
        if (frame[13] != 0x03 || frame[14] != 0x04)
            return false;

        // [2] 状态字符
        status = (char)frame[2] switch
        {
            'S' => ScaleStatus.Stable,
            'U' => ScaleStatus.Weighting,
            'F' => ScaleStatus.OverLoad,
            _ => ScaleStatus.Error,
        };
        if (status == ScaleStatus.Error)
            return false;
        if (status == ScaleStatus.OverLoad)
            return true; // 帧合法，秤超载：无可用重量

        // [3..9] 重量：去空格，逗号换句点
        if (!TryParseAsciiDecimal(frame.Slice(3, 7), out decimal value))
            return false;

        // [10..11] 单位换算
        string unit = GetAscii(frame.Slice(10, 2)).ToLowerInvariant();
        if (unit != "kg" && unit != "g")
            return false;
        weightKg = ToKilograms(value, unit);

        status = RefineStatusByWeight(weightKg, status);
        return true;
    }
}
