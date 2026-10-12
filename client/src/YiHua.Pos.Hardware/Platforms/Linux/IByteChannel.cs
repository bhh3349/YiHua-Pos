using System.Net.Sockets;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// 字节通道抽象：隔离真实 IO（TCP/串口/设备文件），业务逻辑只依赖它，
/// 单测时注入假实现即可断言「发出去的字节序列」，无需真实硬件。
/// </summary>
public interface IByteChannel : IDisposable
{
    /// <summary>当前是否已连接</summary>
    bool IsConnected { get; }

    /// <summary>建立连接</summary>
    Task ConnectAsync(CancellationToken ct);

    /// <summary>写入字节</summary>
    Task WriteAsync(byte[] data, CancellationToken ct);

    /// <summary>
    /// 读取响应（如 DLE EOT 状态查询回包）；超时或无数据返回 0。
    /// </summary>
    Task<int> ReadAsync(byte[] buffer, CancellationToken ct);

    /// <summary>断开连接</summary>
    Task DisconnectAsync();
}

/// <summary>TCP 字节通道（网口打印机，默认 9100 端口）</summary>
public class TcpByteChannel : IByteChannel
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpByteChannel(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsConnected => _client?.Connected == true;

    public async Task ConnectAsync(CancellationToken ct)
    {
        await DisconnectAsync().ConfigureAwait(false);
        _client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        await _client.ConnectAsync(_host, _port, cts.Token).ConfigureAwait(false);
        _stream = _client.GetStream();
        _stream.ReadTimeout = 2000;
        _stream.WriteTimeout = 5000;
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct)
    {
        if (_stream == null)
            throw new InvalidOperationException("TCP 通道未连接");
        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> ReadAsync(byte[] buffer, CancellationToken ct)
    {
        if (_stream == null)
            throw new InvalidOperationException("TCP 通道未连接");
        return await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();
}

/// <summary>设备文件字节通道（USB /dev/usb/lp*、蓝牙 /dev/rfcomm*：打开即写字节）</summary>
public class FileByteChannel : IByteChannel
{
    private readonly string _path;
    private readonly Func<string, Stream> _open;
    private Stream? _stream;

    public FileByteChannel(string path, Func<string, Stream> open)
    {
        _path = path;
        _open = open;
    }

    public bool IsConnected => _stream != null;

    public Task ConnectAsync(CancellationToken ct)
    {
        DisconnectAsync();
        _stream = _open(_path);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct)
    {
        if (_stream == null)
            throw new InvalidOperationException("设备文件未打开");
        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> ReadAsync(byte[] buffer, CancellationToken ct)
    {
        if (_stream == null)
            throw new InvalidOperationException("设备文件未打开");
        return await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _stream = null;
        return Task.CompletedTask;
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();
}
