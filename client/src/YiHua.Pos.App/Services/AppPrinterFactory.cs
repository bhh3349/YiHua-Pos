using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Platforms.Windows;
using YiHua.Pos.Hardware.Printing.EscPos;
using YiHua.Pos.UI.Services.Printing;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.App.Services;

// PrinterStatus / IReceiptPrinter 在 Hardware.Abstractions 和 UI.Services.Settings|Payment
// 各有一份同名类型。这里要的是硬件层那份，显式取别名避免 CS0104。
using PrinterStatus = YiHua.Pos.Hardware.Abstractions.PrinterStatus;
using IReceiptPrinter = YiHua.Pos.Hardware.Abstractions.IReceiptPrinter;

/// <summary>
/// 按打印机配置创建实现。
///
/// Windows 走系统打印队列 RAW 直发（设备名就是「设备和打印机」里的名字）；
/// Linux 走 /dev/usb/lp*、串口、TCP 9100。
/// 目前只接了 Windows —— 交付目标是 win-x64，Linux 那套留着以后用。
/// </summary>
public class AppPrinterFactory : IPrinterFactory
{
    public IReceiptPrinter Create(PrinterConfigDto config)
    {
        var path = config.DevicePath ?? string.Empty;

        // 网络打印机：tcp://ip:port
        if (path.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = path["tcp://".Length..];
            var parts = endpoint.Split(':', 2);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 9100;
            return new NetworkEscPosPrinter(host, port);
        }

        // 串口：COM1..COM8
        if (path.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            return new SerialEscPosPrinter(path);

        // 其余一律当 Windows 已安装打印机名处理
        return new WindowsRawPrinter(path);
    }
}

/// <summary>网络小票机（TCP 9100，ESC/POS 原样发送）。</summary>
public sealed class NetworkEscPosPrinter : IReceiptPrinter
{
    private readonly string _host;
    private readonly int _port;
    private System.Net.Sockets.TcpClient? _client;

    public NetworkEscPosPrinter(string host, int port = 9100)
    {
        _host = host;
        _port = port;
    }

    public string Name => $"{_host}:{_port}";

    public YiHua.Pos.Domain.Enums.PrinterConnection Connection => YiHua.Pos.Domain.Enums.PrinterConnection.Network;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _client?.Dispose();
            _client = new System.Net.Sockets.TcpClient();
            await _client.ConnectAsync(_host, _port).WaitAsync(TimeSpan.FromSeconds(3), ct);
            return _client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }

    public async Task PrintAsync(byte[] escposData, CancellationToken ct = default)
    {
        if (_client is not { Connected: true })
        {
            if (!await ConnectAsync(ct))
                throw new InvalidOperationException($"连不上打印机 {Name}");
        }

        var stream = _client!.GetStream();
        await stream.WriteAsync(escposData, ct);
        await stream.FlushAsync(ct);
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        try
        {
            await PrintAsync(EscPosBuilder.SimpleTestPage(Name), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new PrinterStatus
        {
            IsConnected = _client is { Connected: true },
            IsOnline = _client is { Connected: true },
        });

    public Task OpenCashDrawerAsync(CancellationToken ct = default)
        => PrintAsync(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, ct);
}

/// <summary>串口小票机（老票据机只暴露串口，驱动装不上）。</summary>
public sealed class SerialEscPosPrinter : IReceiptPrinter
{
    private readonly string _portName;
    private System.IO.Ports.SerialPort? _port;

    public SerialEscPosPrinter(string portName)
    {
        _portName = portName;
    }

    public string Name => _portName;

    public YiHua.Pos.Domain.Enums.PrinterConnection Connection => YiHua.Pos.Domain.Enums.PrinterConnection.Serial;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _port?.Dispose();
            _port = new System.IO.Ports.SerialPort(_portName, 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
            _port.Open();
            return Task.FromResult(_port.IsOpen);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync()
    {
        try { _port?.Close(); } catch { /* 已经关了 */ }
        _port?.Dispose();
        _port = null;
        return Task.CompletedTask;
    }

    public Task PrintAsync(byte[] escposData, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true })
            throw new InvalidOperationException($"串口 {_portName} 没有打开");

        _port.Write(escposData, 0, escposData.Length);
        return Task.CompletedTask;
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        try
        {
            await PrintAsync(EscPosBuilder.SimpleTestPage(Name), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new PrinterStatus
        {
            IsConnected = _port is { IsOpen: true },
            IsOnline = _port is { IsOpen: true },
        });

    public Task OpenCashDrawerAsync(CancellationToken ct = default)
        => PrintAsync(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, ct);
}