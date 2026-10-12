// 仓鼠2号 · T-105 电子秤协议
// 协议基类：实现 IScaleProtocol（冻结接口），并提供帧同步与富解析能力。
// 解析均为纯函数（无 IO、无状态），便于单元测试。

using System.Globalization;
using System.Text;
using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Scales.Protocols;

/// <summary>
/// 电子秤协议基类。
/// 派生类只需实现帧格式相关的三个成员：<see cref="FrameLength"/>、
/// <see cref="FindFrameStart"/>、<see cref="TryParseFrame"/>。
/// </summary>
public abstract class ScaleProtocolBase : IScaleProtocol
{
    /// <inheritdoc />
    public abstract string ProtocolName { get; }

    /// <inheritdoc />
    public virtual int BaudRate => 9600;

    /// <inheritdoc />
    public virtual int DataBits => 8;

    /// <inheritdoc />
    public virtual string Parity => "None";

    /// <inheritdoc />
    public virtual string StopBits => "One";

    /// <summary>定长帧的字节数</summary>
    public abstract int FrameLength { get; }

    /// <inheritdoc />
    public bool TryParse(ReadOnlySpan<byte> frame, out decimal weight)
    {
        if (TryParseFrame(frame, out weight, out var status))
        {
            // 帧合法但秤处于错误/超载时，不返回可用重量
            if (status == ScaleStatus.Error || status == ScaleStatus.OverLoad || status == ScaleStatus.Empty)
            {
                weight = 0;
                return false;
            }
            return true;
        }
        weight = 0;
        return false;
    }

    /// <summary>
    /// 富解析：解析一帧，返回重量（kg）与秤状态。
    /// 纯函数，无 IO，可直接单元测试。
    /// </summary>
    /// <returns>帧结构合法返回 true；半帧/帧头错/数字解析失败返回 false</returns>
    public abstract bool TryParseFrame(ReadOnlySpan<byte> frame, out decimal weightKg, out ScaleStatus status);

    /// <inheritdoc />
    public abstract byte[] BuildZeroCommand();

    /// <inheritdoc />
    public abstract byte[] BuildTareCommand();

    /// <summary>构建「取重」指令。被动接收型协议返回空数组。</summary>
    public virtual byte[] BuildTakeWeightCommand() => Array.Empty<byte>();

    /// <summary>构建「初始化」指令。无初始化流程的协议返回 null。</summary>
    public virtual byte[]? BuildInitCommand() => null;

    /// <summary>
    /// 在字节流缓冲中定位一帧完整的帧首。
    /// </summary>
    /// <param name="buffer">已累积的字节流</param>
    /// <returns>帧首下标；没有完整帧时返回 -1（半包，等待更多数据）</returns>
    public abstract int FindFrameStart(ReadOnlySpan<byte> buffer);

    // ------------------------------------------------------------------
    // 共享小工具（protected，供派生类使用）

    /// <summary>ASCII 解码并去首尾空格</summary>
    protected static string GetAscii(ReadOnlySpan<byte> span)
        => Encoding.ASCII.GetString(span).Trim();

    /// <summary>
    /// 解析 ASCII 数字字段（允许前导空格、逗号小数点、前导符号）。
    /// </summary>
    protected static bool TryParseAsciiDecimal(ReadOnlySpan<byte> span, out decimal value)
    {
        string text = Encoding.ASCII.GetString(span).Trim().Replace(',', '.');
        return decimal.TryParse(text,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>按单位换算成 kg（kg×1，g÷1000），保留 3 位小数</summary>
    protected static decimal ToKilograms(decimal value, string unit)
        => Math.Round(unit switch
        {
            "kg" => value,
            "g" => value / 1000m,
            _ => throw new ArgumentException($"未知重量单位: '{unit}'", nameof(unit)),
        }, 3);

    /// <summary>按重量修正状态：0 → 空秤，负数 → 负重量</summary>
    protected static ScaleStatus RefineStatusByWeight(decimal weightKg, ScaleStatus status)
    {
        if (weightKg == 0) return ScaleStatus.UnWeight;
        if (weightKg < 0) return ScaleStatus.NegativeWeight;
        return status;
    }
}
