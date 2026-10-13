using System.Runtime.InteropServices;
using System.Text;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Printing.EscPos;

namespace YiHua.Pos.Hardware.Platforms.Windows;

/// <summary>
/// Windows 小票打印机（走系统打印后台 raw 直发）。
///
/// 为什么走 RAW 而不是驱动渲染：
/// 小票机要的是 ESC/POS 指令流（切纸、开钱箱、二维码都是指令），
/// 走驱动的图形渲染会把指令吃掉，钱箱打不开、切纸也不动。
/// RAW 直发把字节原样交给打印机，不经过驱动的页面渲染。
///
/// 前提：打印机在 Windows 里装好了驱动（通用 Generic / Text Only 即可），
/// 设备名就是「设备和打印机」里看到的那个名字。
/// </summary>
public sealed class WindowsRawPrinter : IReceiptPrinter
{
    private const int PrinterEnumLocal = 2;
    private const int PrinterEnumConnections = 4;

    private readonly string _printerName;

    public WindowsRawPrinter(string printerName, PrinterConnection connection = PrinterConnection.Usb)
    {
        _printerName = printerName;
        Connection = connection;
    }

    public string Name => _printerName;

    public PrinterConnection Connection { get; }

    /// <summary>枚举本机已安装的打印机名（不判断是否在线，离线打印机也会列出来）。</summary>
    public static IReadOnlyList<string> ListInstalledPrinters()
    {
        var names = new List<string>();
        var needed = 0;

        // 第一次调用只为拿所需缓冲区大小（必然返回 false）
        EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 2, IntPtr.Zero, 0, out needed, out _);
        if (needed <= 0)
            return names;

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 2, buffer, needed, out _, out var returned))
                return names;

            var offset = buffer;
            for (var i = 0; i < returned; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo1>(offset);
                if (!string.IsNullOrWhiteSpace(info.pPrinterName))
                    names.Add(info.pPrinterName);
                offset = IntPtr.Add(offset, Marshal.SizeOf<PrinterInfo1>());
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return names;
    }

    /// <summary>默认打印机名（没有就返回空）。</summary>
    public static string GetDefaultPrinterName()
    {
        var sb = new StringBuilder(512);
        var len = sb.Capacity;
        return GetDefaultPrinter(sb, ref len) ? sb.ToString() : string.Empty;
    }

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        // Windows 打印队列是「发出去才算」，没有持久连接的概念。
        // 这里只确认打印机名在系统里存在，避免后面报一个看不懂的错。
        var exists = ListInstalledPrinters()
            .Any(n => string.Equals(n, _printerName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task PrintAsync(byte[] escposData, CancellationToken ct = default)
    {
        if (escposData == null || escposData.Length == 0)
            return Task.CompletedTask;

        var ok = SendBytesToPrinter(_printerName, escposData, out var error);
        if (!ok)
            throw new InvalidOperationException($"发送到打印机「{_printerName}」失败：{error}");

        return Task.CompletedTask;
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        var builder = new EscPosBuilder();
        builder.Init()
               .Align(AlignStyle.Center)
               .Bold(true)
               .TextLine("逸华餐饮 打印测试")
               .Bold(false)
               .TextLine($"打印机：{_printerName}")
               .TextLine($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}")
               .Feed(2)
               .Cut(CutMode.Full);

        await PrintAsync(builder.Build(), ct);
        return true;
    }

    public Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var installed = ListInstalledPrinters()
            .Any(n => string.Equals(n, _printerName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(new PrinterStatus
        {
            IsConnected = installed,
            IsOnline = installed,
            HasPaper = true,   // 纸张状态要双向通信（USB 回读），这里不做假判断
            HasError = !installed,
            ErrorMessage = installed ? string.Empty : $"系统里找不到打印机「{_printerName}」",
        });
    }

    /// <summary>开钱箱：走打印机的 RJ11 口，发 ESC p 指令。</summary>
    public Task OpenCashDrawerAsync(CancellationToken ct = default)
    {
        // ESC p m t1 t2 —— m=0 是 2 号引脚（大多数钱箱）
        var cmd = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        return PrintAsync(cmd, ct);
    }

    // ---------------- winspool.drv ----------------

    private static bool SendBytesToPrinter(string printerName, byte[] data, out string error)
    {
        error = string.Empty;
        IntPtr printerHandle = IntPtr.Zero;
        IntPtr docHandle = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(printerName, out printerHandle, IntPtr.Zero))
            {
                error = $"无法打开打印机「{printerName}」（错误 {Marshal.GetLastWin32Error()}）。请确认已在系统里装好该打印机。";
                return false;
            }

            var docInfo = new DocInfo1
            {
                pDocName = "YiHua POS Receipt",
                pDataType = "RAW",   // 关键：RAW 才会把 ESC/POS 字节原样送下去
            };

            docHandle = StartDocPrinter(printerHandle, 1, ref docInfo);
            if (docHandle == IntPtr.Zero)
            {
                error = $"创建打印任务失败（错误 {Marshal.GetLastWin32Error()}）";
                return false;
            }

            if (!StartPagePrinter(printerHandle))
            {
                error = $"开始打印页失败（错误 {Marshal.GetLastWin32Error()}）";
                return false;
            }

            var unmanaged = Marshal.AllocCoTaskMem(data.Length);
            try
            {
                Marshal.Copy(data, 0, unmanaged, data.Length);
                if (!WritePrinter(printerHandle, unmanaged, data.Length, out var written) || written != data.Length)
                {
                    error = $"写入打印机失败（已写 {written}/{data.Length} 字节）";
                    return false;
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(unmanaged);
                EndPagePrinter(printerHandle);
            }

            EndDocPrinter(printerHandle);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (printerHandle != IntPtr.Zero)
                ClosePrinter(printerHandle);
        }
    }

    /// <summary>
    /// PRINTER_INFO_2W。
    /// ⚠️ 字段顺序必须与 Windows SDK 一致：**pServerName 在 pPrinterName 之前**。
    /// 这两个写反了不会报错，只会静默读到 NULL ——
    /// 表现是「系统里装了打印机，但一台都发现不了」。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo1
    {
        public string? pServerName;
        public string? pPrinterName;
        public string? pShareName;
        public string? pPortName;
        public string? pDriverName;
        public string? pComment;
        public string? pLocation;
        public IntPtr pDevMode;
        public string? pSepFile;
        public string? pPrintProcessor;
        public string? pDatatype;
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo1 pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr buffer, int size, out int needed, out int returned);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);
}

/// <summary>Windows 打印机发现：列出系统里已安装的打印机。</summary>
public sealed class WindowsPrinterDiscovery : IPrinterDiscovery
{
    public Task<IReadOnlyList<PrinterInfo>> DiscoverAsync(CancellationToken ct = default)
    {
        var defaultName = WindowsRawPrinter.GetDefaultPrinterName();

        var list = WindowsRawPrinter.ListInstalledPrinters()
            .Select(name => new PrinterInfo
            {
                Name = name,
                Address = name,
                Model = name,
                // 系统里装好的小票机通常走 USB 或网口；
                // 具体是哪种由用户在设置页选，这里默认按 USB 呈现。
                Connection = PrinterConnection.Usb,
            })
            .ToList();

        // 默认打印机排最前：装机时十有八九就用它
        if (!string.IsNullOrEmpty(defaultName))
        {
            var idx = list.FindIndex(p => string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase));
            if (idx > 0)
            {
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, item);
            }
        }

        return Task.FromResult<IReadOnlyList<PrinterInfo>>(list);
    }
}