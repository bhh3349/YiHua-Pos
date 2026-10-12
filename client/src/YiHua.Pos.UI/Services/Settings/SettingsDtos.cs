using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 门店信息 DTO。
/// </summary>
public class StoreInfoDto
{
    public string StoreName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte[]? LogoData { get; set; }
}

/// <summary>
/// 打印机配置 DTO。
/// </summary>
public class PrinterConfigDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    /// <summary>纸宽：58 / 80 (mm)。</summary>
    public int PaperWidth { get; set; } = 80;
    /// <summary>用途：小票/后厨/标签/交班单。</summary>
    public PrinterUsage Usage { get; set; } = PrinterUsage.Receipt;
    /// <summary>设备路径（Linux: /dev/usb/lp0；Windows: 共享名/COM口）。</summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>是否启用（停用的打印机不参与打印，但配置保留）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 绑定的档口名（如「热菜」「凉菜」）。为空表示不参与档口分单。
    /// 后厨单要能按档口分打到不同打印机，否则厨师互相抢单。
    /// </summary>
    public string Station { get; set; } = string.Empty;

    /// <summary>运行状态：实时探测得到，不持久化。</summary>
    public PrinterStatus Status { get; set; } = PrinterStatus.Unknown;
}

/// <summary>
/// 打印机运行状态。
/// 属于运行时状态而非持久化配置，所以留在 UI 层不进 Domain。
/// </summary>
public enum PrinterStatus
{
    Unknown,
    Online,
    Offline,
    OutOfPaper,
    Error,
}

/// <summary>
/// 收银规则 DTO。RoundingRule 已上移到 Domain（Wave 5 补充）。
/// </summary>
public class CashRuleDto
{
    public RoundingRule RoundingRule { get; set; } = RoundingRule.None;

    public bool AllowItemDiscount { get; set; } = true;
    public bool AllowOrderDiscount { get; set; } = true;
    public bool AllowMemberDiscount { get; set; } = true;

    public bool PrintItems { get; set; } = true;
    public bool PrintDiscount { get; set; } = true;
}

/// <summary>
/// 网络同步 DTO。
/// </summary>
public class SyncInfoDto
{
    public string ServerAddress { get; set; } = "http://localhost:8000";
    public bool IsOnline { get; set; }
    public int PendingCount { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// 配置读写接口（App 层实现持久化，UI 层只依赖接口）。
/// </summary>
public interface ISettingsStore
{
    Task<StoreInfoDto> GetStoreInfoAsync(CancellationToken ct = default);
    Task SaveStoreInfoAsync(StoreInfoDto info, CancellationToken ct = default);
    Task<List<PrinterConfigDto>> GetPrintersAsync(CancellationToken ct = default);
    Task SavePrintersAsync(List<PrinterConfigDto> printers, CancellationToken ct = default);
    Task<CashRuleDto> GetCashRuleAsync(CancellationToken ct = default);
    Task SaveCashRuleAsync(CashRuleDto rule, CancellationToken ct = default);
    Task<SyncInfoDto> GetSyncInfoAsync(CancellationToken ct = default);
    Task SaveSyncInfoAsync(SyncInfoDto info, CancellationToken ct = default);
}
