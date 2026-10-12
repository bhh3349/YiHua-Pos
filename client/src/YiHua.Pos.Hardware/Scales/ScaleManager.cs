// MUSE-2 · T-105 电子秤协议
// ScaleManager：串口枚举 + 自动探测（遍历串口 × 协议，发取重指令看有无合法回包）。
// 探测顺序按市场占有率：大华 → 托利多 → 凯士（见 ScaleProtocolRegistry）。

using System.IO.Ports;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Hardware.Scales;

/// <summary>电子秤连接管理与自动探测</summary>
public sealed class ScaleManager
{
    private readonly ScaleProtocolRegistry _registry;

    public ScaleManager() : this(new ScaleProtocolRegistry()) { }

    public ScaleManager(ScaleProtocolRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>协议注册表（可注册自定义协议）</summary>
    public ScaleProtocolRegistry Registry => _registry;

    /// <summary>枚举本机串口（Linux: /dev/ttyUSB*；Windows: COM*）</summary>
    public IReadOnlyList<string> GetPortNames() => SerialPort.GetPortNames();

    /// <summary>按协议名创建电子秤实例（不连接）</summary>
    public IScale CreateScale(string portName, string protocolName)
    {
        var protocol = _registry.Create(protocolName);
        if (protocol is not ScaleProtocolBase protocolBase)
            throw new InvalidOperationException($"协议 '{protocolName}' 须继承 ScaleProtocolBase");
        return new SerialScale(portName, protocolBase);
    }

    /// <summary>
    /// 自动探测：遍历串口 × 协议组合，发送取重指令，
    /// 500ms 内收到合法帧即命中。返回命中的（串口，协议名）。
    /// </summary>
    public async Task<(string PortName, string ProtocolName)?> DetectAsync(CancellationToken ct = default)
    {
        foreach (var portName in GetPortNames())
        {
            foreach (var protocol in _registry.CreateInDetectOrder())
            {
                ct.ThrowIfCancellationRequested();
                if (await TryDetectOnPortAsync(portName, protocol, ct).ConfigureAwait(false))
                    return (portName, protocol.ProtocolName);
            }
        }
        return null;
    }

    // 在单个串口上用单个协议试探：发取重指令，500ms 内看有无合法帧
    private static async Task<bool> TryDetectOnPortAsync(
        string portName, ScaleProtocolBase protocol, CancellationToken ct)
    {
        var buffer = new ScaleFrameBuffer(protocol.FrameLength);
        try
        {
            using var port = new SerialPort(portName, protocol.BaudRate,
                Parity.None, protocol.DataBits, StopBits.One)
            {
                ReadTimeout = 100,
                WriteTimeout = 500,
            };
            port.Open();

            var takeWeight = protocol.BuildTakeWeightCommand();
            if (takeWeight.Length > 0)
                port.Write(takeWeight, 0, takeWeight.Length);

            var deadline = DateTime.UtcNow.AddMilliseconds(500);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                int available = port.BytesToRead;
                if (available > 0)
                {
                    var chunk = new byte[available];
                    int read = port.Read(chunk, 0, available);
                    if (read > 0)
                    {
                        buffer.Append(chunk.AsSpan(0, read));
                        // span 不跨 await：解析完再让出
                        bool hit = CheckBuffer(buffer, protocol);
                        if (hit)
                            return true;
                    }
                }
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            // 串口打不开 / 无权限 / 被占用：换下一个组合，不抛异常
        }
        return false;
    }

    // 同步方法内完成 span 解析，不跨 await
    private static bool CheckBuffer(ScaleFrameBuffer buffer, ScaleProtocolBase protocol)
    {
        int start = protocol.FindFrameStart(buffer.Buffered);
        if (start < 0)
            return false;
        var frame = buffer.Buffered.Slice(start, protocol.FrameLength);
        bool hit = protocol.TryParse(frame, out _);
        buffer.Consume(start + protocol.FrameLength);
        return hit;
    }
}
