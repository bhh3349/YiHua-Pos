// MUSE-2 · T-105 电子秤协议
// 大华 Aclas PS1X 串口协议。
// 与 OS2X 同属 ACS 系列，帧格式完全一致（定长 15 字节，帧头 01 02，帧尾 03 04），
// 仅协议名不同：独立注册，便于按型号区分与自动探测。

namespace YiHua.Pos.Hardware.Scales.Protocols;

/// <summary>大华 PS1X 电子秤协议（帧格式同 OS2X）</summary>
public sealed class DaHuaPs1xProtocol : DaHuaOs2xProtocol
{
    public override string ProtocolName => "DaHua.PS1X";
}
