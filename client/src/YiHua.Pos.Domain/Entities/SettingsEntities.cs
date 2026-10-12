using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>
/// 门店信息。
///
/// 299 买断模式下，这张表是商家装好软件后必须填的第一份配置：
/// 店名要打到每一张小票上，填错等于每张单都是错的。
/// </summary>
public class StoreInfo
{
    public string StoreName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    /// <summary>店招图片（logo，压缩后的小票友好尺寸）</summary>
    public byte[]? Logo { get; set; }

    /// <summary>logo 的 MIME 类型</summary>
    public string LogoContentType { get; set; } = string.Empty;
}

/// <summary>
/// 打印机配置。
///
/// 一张小票可以配置多台打印机：前台小票 + 后厨分单。
/// 每个档口（KitchenStation）要能指定打给哪台后厨打印机，
/// 否则后厨单只能全打到一个地方，厨师互相抢单。
/// </summary>
public class PrinterConfig
{
    public long Id { get; set; }

    /// <summary>显示名称，如「前台小票机」</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备路径。
    /// Linux：/dev/usb/lp0、/dev/ttyUSB0、/dev/ttyACM0、/dev/rfcomm0、tcp://ip:9100
    /// Windows：共享打印机名、COM 口、\\.\COM3
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    public PrinterConnection Connection { get; set; } = PrinterConnection.Usb;

    /// <summary>纸宽（毫米）：58 或 80，决定版式列数 32 / 48</summary>
    public int PaperWidthMm { get; set; } = 58;

    /// <summary>用途</summary>
    public PrinterUsage Usage { get; set; } = PrinterUsage.Receipt;

    /// <summary>是否启用（停用的打印机不参与打印，但配置保留）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 绑定的档口名（如「热菜」「凉菜」）。为空表示不参与档口分单。
    /// 后厨单要能按档口分打到不同打印机，否则厨师互相抢单。
    /// </summary>
    public string Station { get; set; } = string.Empty;

    /// <summary>
    /// 绑定的档口 ID 列表。为空表示不参与档口分单。
    /// 一台打印机可以服务多个档口。
    /// </summary>
    public List<long> StationIds { get; set; } = new();
}

/// <summary>打印机用途</summary>
public enum PrinterUsage
{
    /// <summary>前台小票</summary>
    Receipt,

    /// <summary>后厨分单</summary>
    Kitchen,

    /// <summary>标签打印</summary>
    Label,

    /// <summary>交班单</summary>
    Shift,
}

/// <summary>
/// 系统级配置（单行表）。
/// 服务器地址放这里而不是写死，是为了让「域名上线」时只改配置不改代码。
/// </summary>
public class SystemConfig
{
    /// <summary>单机模式 = true 时不同步到服务器</summary>
    public bool OfflineMode { get; set; } = true;

    /// <summary>服务器地址（域名，不要填 IP —— IP 会过期被回收）</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>门店标识（多租户时区分是哪家店）</summary>
    public long StoreId { get; set; }
}

/// <summary>
/// 收银规则。
///
/// 抹零规则必须显式指定取整方式：Math.Round 默认是 ToEven（银行家舍入），
/// 9.995 会变成 9.99 而不是 10.00，收银员会觉得系统算错了。
/// </summary>
public class CashRule
{
    /// <summary>抹零方式</summary>
    public RoundingRule Rounding { get; set; } = RoundingRule.None;

    /// <summary>是否启用单品折扣</summary>
    public bool AllowItemDiscount { get; set; } = true;

    /// <summary>是否启用整单折扣</summary>
    public bool AllowOrderDiscount { get; set; } = true;

    /// <summary>是否启用会员折扣</summary>
    public bool AllowMemberDiscount { get; set; } = true;

    /// <summary>小票是否打印明细</summary>
    public bool PrintItems { get; set; } = true;

    /// <summary>小票是否打印折扣明细</summary>
    public bool PrintDiscount { get; set; } = true;
}

/// <summary>抹零规则</summary>
public enum RoundingRule
{
    /// <summary>不抹零</summary>
    None,

    /// <summary>抹到角（分）</summary>
    ToJiao,

    /// <summary>抹到元</summary>
    ToYuan,

    /// <summary>抹到角但不找零（顾客仍需付分）</summary>
    DiscountToJiao,
}