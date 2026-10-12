// 仓鼠2号 · T-105 电子秤协议
// 凯士 CAS216 串口协议（定长 12 字节，被动接收）。
// 帧格式（依据《协议规范-电子秤.md》4.2）：
//   [0..7]  重量 ASCII（8 字节，空格左填充），例 "   1.250"
//   [8..9]  单位 ASCII（去空格后匹配 "g"/"kg"）
//   [10..11] 保留（本实现不校验）
// 特性：秤主动推送，不发送取重指令；不支持置零/去皮。

namespace YiHua.Pos.Hardware.Scales.Protocols;

/// <summary>凯士 CAS216 电子秤协议（被动接收）</summary>
public class KssCas216Protocol : ScaleProtocolBase
{
    public override string ProtocolName => "CAS.CAS216";
    public override int FrameLength => 12;

    // 被动接收：无取重/置零/去皮指令
    public override byte[] BuildZeroCommand() => Array.Empty<byte>();
    public override byte[] BuildTareCommand() => Array.Empty<byte>();

    /// <summary>
    /// 无帧头帧尾：滑动窗口找第一个「重量可解析且单位已知」的 12 字节，
    /// 找不到返回 -1（噪声，等待更多数据或由缓冲丢弃最旧字节）。
    /// </summary>
    public override int FindFrameStart(ReadOnlySpan<byte> buffer)
    {
        for (int i = 0; i + FrameLength <= buffer.Length; i++)
        {
            if (IsPlausibleFrame(buffer.Slice(i, FrameLength)))
                return i;
        }
        return -1;
    }

    /// <summary>重量字段可解析为数字且单位为 g/kg 时视为合法帧</summary>
    private static bool IsPlausibleFrame(ReadOnlySpan<byte> frame)
    {
        if (!TryParseAsciiDecimal(frame.Slice(0, 8), out _))
            return false;
        string unit = GetAscii(frame.Slice(8, 2)).ToLowerInvariant();
        return unit == "kg" || unit == "g";
    }

    public override bool TryParseFrame(ReadOnlySpan<byte> frame, out decimal weightKg, out ScaleStatus status)
    {
        weightKg = 0;
        status = ScaleStatus.Error;

        if (frame.Length != FrameLength)
            return false;
        if (!IsPlausibleFrame(frame))
            return false;

        TryParseAsciiDecimal(frame.Slice(0, 8), out decimal value);
        string unit = GetAscii(frame.Slice(8, 2)).ToLowerInvariant();
        weightKg = ToKilograms(value, unit);

        // CAS216 无稳定标志位：推送即当前值，视为稳定
        status = RefineStatusByWeight(weightKg, ScaleStatus.Stable);
        return true;
    }
}
