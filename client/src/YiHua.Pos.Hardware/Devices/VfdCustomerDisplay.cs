using System.IO.Ports;
using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Devices;

/// <summary>
/// VFD 真空荧光客显（串口，默认 9600 8N1）。
/// 布局（20 列 × 2 行）：
///   行1：商品名（截断到 20 列）
///   行2：金额行，右对齐
/// </summary>
public sealed class VfdCustomerDisplay : ICustomerDisplay, IDisposable
{
    public const int Columns = 20;
    public const int Rows = 2;

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly VfdTextEncoding _encoding;
    private readonly Action<string>? _log;
    private SerialPort? _port;
    private bool _disposed;

    public VfdCustomerDisplay(
        string portName,
        int baudRate = 9600,
        VfdTextEncoding encoding = VfdTextEncoding.Ascii,
        Action<string>? log = null)
    {
        _portName = string.IsNullOrWhiteSpace(portName)
            ? throw new ArgumentException("串口名不能为空", nameof(portName))
            : portName;
        _baudRate = baudRate;
        _encoding = encoding;
        _log = log;
    }

    public bool IsOpen => _port?.IsOpen == true;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            ClosePort();
            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
            };
            _port.Open();
            Write(new VfdCommandBuilder(_encoding).Init().Clear().Build());
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[VFD] 连接 {_portName} 失败: {ex.Message}");
            ClosePort();
            return Task.FromResult(false);
        }
    }

    public Task ShowAsync(CustomerDisplayData data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (data == null) throw new ArgumentNullException(nameof(data));

        var builder = new VfdCommandBuilder(_encoding);
        builder.Clear();
        builder.SetCursor(0, 0);
        builder.Text(VfdCommandBuilder.TruncateByColumns(data.ItemName ?? string.Empty, Columns));
        builder.SetCursor(1, 0);
        builder.Text(FormatAmountRow(data));
        Write(builder.Build());
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Write(new VfdCommandBuilder(_encoding).Clear().Build());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 金额行（20 列，右对齐）。
    /// ASCII 模式：Tot:128.00 Chg:12.00；GB18030 模式：合计:128.00 找零:12.00。
    /// </summary>
    public string FormatAmountRow(CustomerDisplayData data)
    {
        string row = _encoding == VfdTextEncoding.Gb18030
            ? $"合计:{data.Total:F2} 找零:{data.Change:F2}"
            : $"Tot:{data.Total:F2} Chg:{data.Change:F2}";

        if (_encoding == VfdTextEncoding.Ascii)
            row = VfdCommandBuilder.Sanitize(row);

        if (row.Length > Columns)
            row = row.Substring(row.Length - Columns); // 太长时保留右侧（金额优先）
        return row.PadLeft(Columns);
    }

    private void Write(byte[] bytes)
    {
        var port = _port;
        if (port == null || !port.IsOpen)
            throw new InvalidOperationException("VFD 未连接，请先调用 ConnectAsync");
        port.Write(bytes, 0, bytes.Length);
    }

    private void ClosePort()
    {
        try { _port?.Close(); } catch { /* ignore */ }
        try { _port?.Dispose(); } catch { /* ignore */ }
        _port = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClosePort();
    }
}
