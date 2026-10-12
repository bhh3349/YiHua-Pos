// 仓鼠2号 · T-105 电子秤协议
// SerialScale：IScale 的串口实现（冻结接口，实现放在 仓鼠2号 独占目录）。
// 职责：串口读写 + 帧缓冲（粘包/半包） + 调用协议纯函数解析。
// 解析与 IO 分离：解析逻辑全在 Protocols/*，此处不碰字节语义。

using System.IO.Ports;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Hardware.Scales;

/// <summary>电子秤的串口实现</summary>
public sealed class SerialScale : IScale, IDisposable
{
    private readonly string _portName;
    private readonly ScaleProtocolBase _protocol;
    private readonly ScaleFrameBuffer _buffer;
    private SerialPort? _port;
    private bool _disposed;

    private decimal _lastWeight;
    private ScaleStatus _lastStatus = ScaleStatus.Empty;
    private bool _hasReading;

    public SerialScale(string portName, ScaleProtocolBase protocol)
    {
        _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _buffer = new ScaleFrameBuffer(protocol.FrameLength);
    }

    /// <summary>当前绑定的协议（便于上层查询）</summary>
    public IScaleProtocol Protocol => _protocol;

    /// <inheritdoc />
    public string Name => $"{_protocol.ProtocolName}@{_portName}";

    /// <inheritdoc />
    public bool IsConnected => _port?.IsOpen == true;

    /// <inheritdoc />
    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (IsConnected)
            return Task.FromResult(true);

        var parity = Enum.Parse<Parity>(_protocol.Parity, ignoreCase: true);
        var stopBits = Enum.Parse<StopBits>(_protocol.StopBits, ignoreCase: true);
        _port = new SerialPort(_portName, _protocol.BaudRate, parity, _protocol.DataBits, stopBits)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
        };
        _port.Open();

        // 大华等协议连接后需要发初始化指令
        var init = _protocol.BuildInitCommand();
        if (init is { Length: > 0 })
            _port.Write(init, 0, init.Length);

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task DisconnectAsync()
    {
        if (_port != null)
        {
            try { _port.Close(); } catch { /* 忽略关闭异常 */ }
            _port.Dispose();
            _port = null;
        }
        _buffer.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 取当前重量（kg）。主动式协议先发取重指令再读；被动式（CAS216）直接读推送。
    /// 最多等待 500ms，返回期间内最后一次解析到的重量。
    /// </summary>
    public async Task<decimal> GetWeightAsync(CancellationToken ct = default)
    {
        EnsureConnected();

        var takeWeight = _protocol.BuildTakeWeightCommand();
        if (takeWeight.Length > 0)
            _port!.Write(takeWeight, 0, takeWeight.Length);

        var deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            PumpAvailable();
            if (_hasReading)
                break;
            await Task.Delay(20, ct).ConfigureAwait(false);
        }
        return _lastWeight;
    }

    /// <summary>最近一次读数是否稳定</summary>
    public async Task<bool> IsStableAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        PumpAvailable();
        await Task.CompletedTask.ConfigureAwait(false);
        return _hasReading && _lastStatus == ScaleStatus.Stable;
    }

    /// <inheritdoc />
    public Task ZeroAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        var cmd = _protocol.BuildZeroCommand();
        if (cmd.Length == 0)
            throw new InvalidOperationException($"{_protocol.ProtocolName} 不支持置零");
        _port!.Write(cmd, 0, cmd.Length);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TareAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        var cmd = _protocol.BuildTareCommand();
        if (cmd.Length == 0)
            throw new InvalidOperationException($"{_protocol.ProtocolName} 不支持去皮");
        _port!.Write(cmd, 0, cmd.Length);
        return Task.CompletedTask;
    }

    /// <summary>最近一次解析状态（供 UI 显示晃动/超载等）</summary>
    public ScaleStatus LastStatus => _lastStatus;

    // 把串口可读字节泵入缓冲，并逐帧解析
    private void PumpAvailable()
    {
        if (_port == null || !_port.IsOpen)
            return;
        int available;
        try { available = _port.BytesToRead; }
        catch { return; }
        if (available <= 0)
            return;

        var chunk = new byte[available];
        int read;
        try { read = _port.Read(chunk, 0, available); }
        catch (TimeoutException) { return; }
        if (read <= 0)
            return;

        _buffer.Append(chunk.AsSpan(0, read));

        while (true)
        {
            var buffered = _buffer.Buffered;
            int start = _protocol.FindFrameStart(buffered);
            if (start < 0)
                break;
            var frame = buffered.Slice(start, _protocol.FrameLength);
            _buffer.Consume(start + _protocol.FrameLength);
            if (_protocol.TryParseFrame(frame, out var weight, out var status))
            {
                // 去重：数据未变化时不重复上报（《协议规范》1.2 节）
                if (!_hasReading || weight != _lastWeight || status != _lastStatus)
                {
                    _lastWeight = weight;
                    _lastStatus = status;
                    _hasReading = true;
                }
            }
            // 解析失败的帧直接丢弃，继续找下一帧；不抛异常
        }
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected)
            throw new InvalidOperationException("电子秤未连接，请先调用 ConnectAsync");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SerialScale));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
