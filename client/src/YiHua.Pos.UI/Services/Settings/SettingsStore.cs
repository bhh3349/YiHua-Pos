namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 内存配置存储（假实现，用于测试和 UI 开发）。
/// 正式实现由 App 层用仓储完成。
/// </summary>
public class InMemorySettingsStore : ISettingsStore
{
    private StoreInfoDto _storeInfo = new() { StoreName = "示例餐厅" };
    private List<PrinterConfigDto> _printers = new();
    private CashRuleDto _cashRule = new();
    private SyncInfoDto _syncInfo = new();

    public Task<StoreInfoDto> GetStoreInfoAsync(CancellationToken ct = default)
        => Task.FromResult(_storeInfo);

    public Task SaveStoreInfoAsync(StoreInfoDto info, CancellationToken ct = default)
    {
        _storeInfo = info;
        return Task.CompletedTask;
    }

    public Task<List<PrinterConfigDto>> GetPrintersAsync(CancellationToken ct = default)
        => Task.FromResult(_printers.ToList());

    public Task SavePrintersAsync(List<PrinterConfigDto> printers, CancellationToken ct = default)
    {
        _printers = printers.ToList();
        return Task.CompletedTask;
    }

    public Task<CashRuleDto> GetCashRuleAsync(CancellationToken ct = default)
        => Task.FromResult(_cashRule);

    public Task SaveCashRuleAsync(CashRuleDto rule, CancellationToken ct = default)
    {
        _cashRule = rule;
        return Task.CompletedTask;
    }

    public Task<SyncInfoDto> GetSyncInfoAsync(CancellationToken ct = default)
        => Task.FromResult(_syncInfo);

    public Task SaveSyncInfoAsync(SyncInfoDto info, CancellationToken ct = default)
    {
        _syncInfo = info;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 发现的打印机设备 DTO。
/// </summary>
public class DiscoveredPrinterDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string DevicePath { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
}

/// <summary>
/// 打印机自动发现：按平台列出可能的设备路径。
/// Linux: /dev/usb/lp*, /dev/ttyUSB*, /dev/ttyACM*, /dev/rfcomm*, TCP 9100
/// Windows: 共享打印机名, COM口, USB
/// </summary>
public static class PrinterDiscovery
{
    /// <summary>
    /// 发现本机可能的打印机设备。
    /// </summary>
    public static List<DiscoveredPrinterDto> Discover()
    {
        var results = new List<DiscoveredPrinterDto>();

        if (OperatingSystem.IsLinux())
        {
            SafeAddUsbPrinters(results);
            // 网络打印机（TCP 9100 是常见端口，需手动配 IP）
            results.Add(new DiscoveredPrinterDto { DisplayName = "网络打印机（需填 IP）", DevicePath = "tcp://192.168.1.100:9100", Kind = "网络" });
        }
        else if (OperatingSystem.IsWindows())
        {
            // Windows：COM 口
            for (int i = 1; i <= 8; i++)
            {
                var port = $"COM{i}";
                results.Add(new DiscoveredPrinterDto { DisplayName = $"串口 {port}", DevicePath = port, Kind = "串口" });
            }
            results.Add(new DiscoveredPrinterDto { DisplayName = "USB 打印机（系统已安装）", DevicePath = "USB", Kind = "USB" });
            results.Add(new DiscoveredPrinterDto { DisplayName = "网络共享打印机（需填共享名）", DevicePath = @"\\SERVER\Printer", Kind = "网络" });
        }
        else if (OperatingSystem.IsMacOS())
        {
            SafeAddMacPrinters(results);
            results.Add(new DiscoveredPrinterDto { DisplayName = "网络打印机（需填 IP）", DevicePath = "tcp://192.168.1.100:9100", Kind = "网络" });
        }

        return results;
    }

    private static void SafeAddUsbPrinters(List<DiscoveredPrinterDto> results)
    {
        try
        {
            if (Directory.Exists("/dev/usb"))
                foreach (var path in Directory.GetFiles("/dev/usb", "lp*"))
                    results.Add(new DiscoveredPrinterDto { DisplayName = $"USB 打印机 ({Path.GetFileName(path)})", DevicePath = path, Kind = "USB" });
            foreach (var path in SafeGetFiles("/dev", "ttyUSB*"))
                results.Add(new DiscoveredPrinterDto { DisplayName = $"串口打印机 ({Path.GetFileName(path)})", DevicePath = path, Kind = "串口" });
            foreach (var path in SafeGetFiles("/dev", "ttyACM*"))
                results.Add(new DiscoveredPrinterDto { DisplayName = $"串口打印机 ({Path.GetFileName(path)})", DevicePath = path, Kind = "串口" });
            foreach (var path in SafeGetFiles("/dev", "rfcomm*"))
                results.Add(new DiscoveredPrinterDto { DisplayName = $"蓝牙打印机 ({Path.GetFileName(path)})", DevicePath = path, Kind = "蓝牙" });
        }
        catch { /* 设备目录不可访问时跳过，不崩 */ }
    }

    private static void SafeAddMacPrinters(List<DiscoveredPrinterDto> results)
    {
        try
        {
            foreach (var path in SafeGetFiles("/dev", "tty.usbserial*"))
                results.Add(new DiscoveredPrinterDto { DisplayName = $"USB 串口 ({Path.GetFileName(path)})", DevicePath = path, Kind = "串口" });
        }
        catch { }
    }

    private static IEnumerable<string> SafeGetFiles(string dir, string pattern)
    {
        try { return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>
    /// 校验设备路径格式（按平台）。
    /// </summary>
    public static bool IsValidDevicePath(string path, out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "设备路径不能为空";
            return false;
        }

        var p = path.Trim();
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            if (p.StartsWith("/dev/") || p.StartsWith("tcp://"))
            {
                error = null;
                return true;
            }
            error = "Linux/macOS 下设备路径应为 /dev/ 开头（如 /dev/usb/lp0）或 tcp:// 开头";
            return false;
        }

        // Windows
        if (p.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(@"\\"))
        {
            error = null;
            return true;
        }
        error = "Windows 下设备路径应为 COM口（如 COM1）、USB 或网络共享（如 \\\\SERVER\\Printer）";
        return false;
    }
}
