using System.IO.Ports;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// 串口抽象：对 <see cref="SerialPort"/> 的最小封装，便于单测注入假实现、
/// 断言波特率/数据位/校验位/停止位等参数配置正确。
/// </summary>
public interface ISerialPort : IDisposable
{
    string PortName { get; set; }
    int BaudRate { get; set; }
    int DataBits { get; set; }
    Parity Parity { get; set; }
    StopBits StopBits { get; set; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    bool IsOpen { get; }
    void Open();
    void Close();
    void Write(byte[] buffer, int offset, int count);
    int Read(byte[] buffer, int offset, int count);
}

/// <summary><see cref="ISerialPort"/> 的真实实现（包装 BCL 的 SerialPort）</summary>
public class SerialPortAdapter : ISerialPort
{
    private readonly SerialPort _inner = new();

    public string PortName { get => _inner.PortName; set => _inner.PortName = value; }
    public int BaudRate { get => _inner.BaudRate; set => _inner.BaudRate = value; }
    public int DataBits { get => _inner.DataBits; set => _inner.DataBits = value; }
    public Parity Parity { get => _inner.Parity; set => _inner.Parity = value; }
    public StopBits StopBits { get => _inner.StopBits; set => _inner.StopBits = value; }
    public int ReadTimeout { get => _inner.ReadTimeout; set => _inner.ReadTimeout = value; }
    public int WriteTimeout { get => _inner.WriteTimeout; set => _inner.WriteTimeout = value; }
    public bool IsOpen => _inner.IsOpen;

    public void Open() => _inner.Open();
    public void Close() => _inner.Close();
    public void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// 串口字节通道：把 <see cref="ISerialPort"/> 适配为 <see cref="IByteChannel"/>。
/// 构造时即固定通信参数（9600-8-N-1 为小票打印机常见默认值，可覆盖波特率）。
/// </summary>
public class SerialByteChannel : IByteChannel
{
    private readonly ISerialPort _port;

    public SerialByteChannel(ISerialPort port, string portName, int baudRate = 9600)
    {
        _port = port;
        _port.PortName = portName;
        _port.BaudRate = baudRate;
        _port.DataBits = 8;
        _port.Parity = Parity.None;
        _port.StopBits = StopBits.One;
        _port.ReadTimeout = 2000;
        _port.WriteTimeout = 5000;
    }

    public bool IsConnected => _port.IsOpen;

    public Task ConnectAsync(CancellationToken ct)
    {
        _port.Open();
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data, CancellationToken ct)
    {
        if (!_port.IsOpen)
            throw new InvalidOperationException("串口未打开");
        _port.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    public Task<int> ReadAsync(byte[] buffer, CancellationToken ct)
    {
        if (!_port.IsOpen)
            throw new InvalidOperationException("串口未打开");
        try
        {
            return Task.FromResult(_port.Read(buffer, 0, buffer.Length));
        }
        catch (TimeoutException)
        {
            return Task.FromResult(0);
        }
    }

    public Task DisconnectAsync()
    {
        if (_port.IsOpen)
            _port.Close();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _port.Dispose();
    }
}
