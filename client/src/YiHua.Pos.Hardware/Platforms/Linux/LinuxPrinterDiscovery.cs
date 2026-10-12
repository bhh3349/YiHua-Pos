using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 打印机设备发现：
/// - USB（/dev/usb/lp*）：usblp 驱动只绑定打印机，直接认定；
/// - 串口（ttyUSB*/ttyS*/ttyACM*）与蓝牙（/dev/rfcomm*）：逐个探测，
///   发送 DLE EOT 1 状态查询，≤300ms 内有回包即认定为打印机，排除普通串口设备。
/// 枚举与探测均可注入假实现，单测不依赖真实硬件。
/// </summary>
public class LinuxPrinterDiscovery : IPrinterDiscovery
{
    private readonly Func<IEnumerable<string>> _serialEnumerator;
    private readonly Func<IEnumerable<string>> _usbEnumerator;
    private readonly Func<IEnumerable<string>> _bluetoothEnumerator;
    private readonly Func<string, CancellationToken, Task<bool>> _probe;

    public LinuxPrinterDiscovery(
        Func<IEnumerable<string>>? serialEnumerator = null,
        Func<IEnumerable<string>>? usbEnumerator = null,
        Func<IEnumerable<string>>? bluetoothEnumerator = null,
        Func<string, CancellationToken, Task<bool>>? probe = null)
    {
        _serialEnumerator = serialEnumerator ?? LinuxSerialPortEnumerator.GetSerialPorts;
        _usbEnumerator = usbEnumerator ?? EnumerateUsbPrinters;
        _bluetoothEnumerator = bluetoothEnumerator ?? EnumerateBluetoothDevices;
        _probe = probe ?? ProbeSerialPrinterAsync;
    }

    public async Task<IReadOnlyList<PrinterInfo>> DiscoverAsync(CancellationToken ct = default)
    {
        var result = new List<PrinterInfo>();

        foreach (var dev in SafeEnumerate(_usbEnumerator))
        {
            result.Add(new PrinterInfo
            {
                Name = $"USB 打印机 ({dev})",
                Connection = PrinterConnection.Usb,
                Address = dev,
                Model = "USB 打印机",
            });
        }

        foreach (var dev in SafeEnumerate(_bluetoothEnumerator))
        {
            if (await _probe(dev, ct).ConfigureAwait(false))
            {
                result.Add(new PrinterInfo
                {
                    Name = $"蓝牙打印机 ({dev})",
                    Connection = PrinterConnection.Bluetooth,
                    Address = dev,
                    Model = "蓝牙打印机",
                });
            }
        }

        foreach (var dev in SafeEnumerate(_serialEnumerator))
        {
            if (await _probe(dev, ct).ConfigureAwait(false))
            {
                result.Add(new PrinterInfo
                {
                    Name = $"串口打印机 ({dev})",
                    Connection = PrinterConnection.Serial,
                    Address = dev,
                    Model = "串口打印机",
                });
            }
        }

        return result;
    }

    private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> enumerator)
    {
        try
        {
            return enumerator();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateUsbPrinters()
    {
        const string dir = "/dev/usb";
        if (!Directory.Exists(dir))
            return Enumerable.Empty<string>();
        try
        {
            return Directory.GetFiles(dir, "lp*");
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateBluetoothDevices()
    {
        const string dir = "/dev";
        try
        {
            return Directory.GetFiles(dir, "rfcomm*");
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// 串口打印机探测：打开串口（9600-8-N-1），发送 DLE EOT 1，
    /// 300ms 内收到回包即判定为打印机。
    /// </summary>
    private static async Task<bool> ProbeSerialPrinterAsync(string device, CancellationToken ct)
    {
        try
        {
            using var port = new SerialPortAdapter
            {
                PortName = device,
                BaudRate = 9600,
                DataBits = 8,
                Parity = System.IO.Ports.Parity.None,
                StopBits = System.IO.Ports.StopBits.One,
                ReadTimeout = 300,
                WriteTimeout = 1000,
            };
            port.Open();
            try
            {
                port.Write(new byte[] { 0x10, 0x04, 0x01 }, 0, 3); // DLE EOT 1
                var buf = new byte[1];
                int read = await Task.Run(() =>
                {
                    try
                    {
                        return port.Read(buf, 0, 1);
                    }
                    catch (TimeoutException)
                    {
                        return 0;
                    }
                }, ct).ConfigureAwait(false);
                return read > 0;
            }
            finally
            {
                port.Close();
            }
        }
        catch
        {
            // 打不开 / 发不出 / 超时：不是打印机（或不可用），直接排除
            return false;
        }
    }
}
