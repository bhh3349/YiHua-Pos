namespace YiHua.Pos.UI.Services.Report;

/// <summary>
/// 报表用订单 DTO（UI 层本地形状）。
/// App 层将 Domain.Order 映射为此类型（字段一一对应）。
/// </summary>
public class ReportOrderDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public long DeskId { get; set; }
    public string DeskName { get; set; } = string.Empty;
    public int OrderType { get; set; } // 1=堂食 2=外带 3=外卖 4=自提
    public int Status { get; set; }    // 0=待结账 1=已结账 2=已退单 3=作废
    public decimal AmountTotal { get; set; }
    public decimal AmountDiscount { get; set; }
    public decimal AmountPay { get; set; }
    public decimal AmountRefund { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal AmountChange { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SettleTime { get; set; }
    public List<ReportOrderItemDto> Items { get; } = new();
    public List<ReportPaymentDto> Payments { get; } = new();

    /// <summary>归属时间：结账时间优先，否则下单时间（跨天订单规则）。</summary>
    public DateTime AttributedTime => SettleTime ?? CreatedAt;

    public bool IsVoid => Status == 3;
}

public class ReportOrderItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public bool IsGift { get; set; }
    public bool IsRefunded { get; set; }
}

public class ReportPaymentDto
{
    public int PayType { get; set; } // 1=现金 2=自定义 3=会员 90/91=预留
    public string PayTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// 时段桶。
/// </summary>
public class HourBucket
{
    public int Hour { get; set; } // 0-23
    public decimal Turnover { get; set; }
    public int OrderCount { get; set; }
    public bool IsPeak { get; set; }
    /// <summary>占全天最大值的比例（0-1，用于条形图）。</summary>
    public double BarRatio { get; set; }
}

/// <summary>
/// 排行项（菜品/桌台通用）。
/// </summary>
public class RankItem
{
    public string Name { get; set; } = string.Empty;
    public long Id { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    /// <summary>占比（0~1，分母为0时为0）。</summary>
    public decimal Share { get; set; }
    public int Rank { get; set; }
}
