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
    /// <summary>档口（后厨打印机用，如"热菜"/"凉菜"）。</summary>
    public string Station { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public PrinterStatus Status { get; set; } = PrinterStatus.Unknown;
}

public enum PrinterUsage
{
    Receipt,   // 小票
    Kitchen,   // 后厨
    Label,     // 标签
    Shift,     // 交班单
}

public enum PrinterStatus
{
    Unknown,
    Online,
    Offline,
    OutOfPaper,
    Error,
}

/// <summary>
/// 收银规则 DTO。
/// </summary>
public class CashRuleDto
{
    public RoundingRule RoundingRule { get; set; } = RoundingRule.None;
    public bool AllowItemDiscount { get; set; } = true;
    public bool AllowOrderDiscount { get; set; } = true;
    public bool ShowDetailOnReceipt { get; set; } = true;
    public bool ShowDiscountOnReceipt { get; set; } = true;
    public bool ShowQrCodeOnReceipt { get; set; } = false;
}

public enum RoundingRule
{
    None,       // 不抹零
    ToJiao,     // 抹到角
    ToYuan,     // 抹到元
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
