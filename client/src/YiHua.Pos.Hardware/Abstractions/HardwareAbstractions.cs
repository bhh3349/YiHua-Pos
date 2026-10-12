using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Hardware.Abstractions;

// ============================================================================
//  硬件抽象接口 —— 【冻结文件】由 Lead 维护，实现者不得修改
//
//  实现者只实现这些接口，放在各自的目录：
//    Linux  实现 → Hardware/Platforms/Linux/**
//    Windows 实现 → Hardware/Platforms/Windows/**
// ============================================================================

/// <summary>打印机状态</summary>
public class PrinterStatus
{
    public bool IsConnected { get; set; }
    public bool IsOnline { get; set; }
    public bool HasPaper { get; set; } = true;
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int PaperWidthMm { get; set; } = 80;
}

/// <summary>打印机信息（用于设备发现）</summary>
public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public PrinterConnection Connection { get; set; }

    /// <summary>USB: VID_PID 或设备路径；Serial: COM1/ttyUSB0；Network: ip:port</summary>
    public string Address { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// 小票/标签打印机。
/// 实现要点：**不需要厂商驱动**，只要能往设备写字节（ESC/POS 指令）
/// </summary>
public interface IReceiptPrinter
{
    string Name { get; }
    PrinterConnection Connection { get; }

    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    /// <summary>发送原始 ESC/POS 字节流</summary>
    Task PrintAsync(byte[] escposData, CancellationToken ct = default);

    /// <summary>打印测试页</summary>
    Task<bool> TestAsync(CancellationToken ct = default);

    Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>开钱箱（走打印机 RJ11 口）</summary>
    Task OpenCashDrawerAsync(CancellationToken ct = default);
}

/// <summary>打印机发现（平台相关）</summary>
public interface IPrinterDiscovery
{
    /// <summary>枚举本机可用打印机</summary>
    Task<IReadOnlyList<PrinterInfo>> DiscoverAsync(CancellationToken ct = default);
}

/// <summary>电子秤</summary>
public interface IScale
{
    string Name { get; }
    bool IsConnected { get; }

    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    /// <summary>取当前重量（单位：kg，稳定值）</summary>
    Task<decimal> GetWeightAsync(CancellationToken ct = default);

    /// <summary>是否稳定（未在晃动）</summary>
    Task<bool> IsStableAsync(CancellationToken ct = default);

    Task ZeroAsync(CancellationToken ct = default);
    Task TareAsync(CancellationToken ct = default);
}

/// <summary>电子秤协议（每种品牌一个实现）</summary>
public interface IScaleProtocol
{
    /// <summary>协议名，如 "DaHua.OS2X"</summary>
    string ProtocolName { get; }

    /// <summary>串口参数</summary>
    int BaudRate { get; }
    int DataBits { get; }
    string Parity { get; }
    string StopBits { get; }

    /// <summary>尝试解析一帧数据，成功返回重量（kg）</summary>
    bool TryParse(ReadOnlySpan<byte> frame, out decimal weight);

    /// <summary>构建「置零」指令</summary>
    byte[] BuildZeroCommand();

    /// <summary>构建「去皮」指令</summary>
    byte[] BuildTareCommand();
}

/// <summary>钱箱</summary>
public interface ICashDrawer
{
    Task OpenAsync(CancellationToken ct = default);
}

/// <summary>客显数据</summary>
public class CustomerDisplayData
{
    /// <summary>商品名（左）</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>单价</summary>
    public decimal Price { get; set; }

    /// <summary>数量</summary>
    public decimal Qty { get; set; }

    /// <summary>合计</summary>
    public decimal Total { get; set; }

    /// <summary>实收</summary>
    public decimal Received { get; set; }

    /// <summary>找零</summary>
    public decimal Change { get; set; }

    /// <summary>欢迎语 / 提示语</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>客显（VFD / LCD）</summary>
public interface ICustomerDisplay
{
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task ShowAsync(CustomerDisplayData data, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

/// <summary>副屏（应用自渲染，通常是第二块 HDMI 屏）</summary>
public interface ISecondaryScreen
{
    /// <summary>是否可用（是否检测到第二块屏）</summary>
    bool IsAvailable { get; }

    Task ShowOrderAsync(object order, CancellationToken ct = default);
    Task ShowIdleAsync(CancellationToken ct = default);
    Task ShowAdAsync(string imagePath, CancellationToken ct = default);
}
