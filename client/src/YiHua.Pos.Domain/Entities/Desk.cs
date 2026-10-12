using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>桌台区域</summary>
public class DeskArea
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Sort { get; set; }
    public long Version { get; set; } = 1;
}

/// <summary>桌台</summary>
public class Desk
{
    public long Id { get; set; }
    public long AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>座位数</summary>
    public int Capacity { get; set; } = 4;

    public DeskStatus Status { get; set; } = DeskStatus.Idle;

    /// <summary>当前订单 ID（0 表示无）</summary>
    public long CurrentOrderId { get; set; }

    /// <summary>当前就餐人数</summary>
    public int GuestCount { get; set; }

    /// <summary>开台时间</summary>
    public DateTime? OpenTime { get; set; }

    /// <summary>当前消费金额（列表展示用）</summary>
    public decimal CurrentAmount { get; set; }

    public int Sort { get; set; }
    public long Version { get; set; } = 1;

    public bool IsIdle => Status == DeskStatus.Idle;
}

/// <summary>交班记录</summary>
public class Shift
{
    public long Id { get; set; }
    public string TerminalNo { get; set; } = string.Empty;
    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;

    public DateTime StartAt { get; set; } = DateTime.Now;
    public DateTime? EndAt { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    /// <summary>开班备用金</summary>
    public decimal CashOpen { get; set; }

    /// <summary>现金应收（系统计算）</summary>
    public decimal CashShould { get; set; }

    /// <summary>现金实收（人工清点）</summary>
    public decimal CashActual { get; set; }

    /// <summary>差异 = 实收 - 应收</summary>
    public decimal CashDiff { get; set; }

    /// <summary>本班订单数</summary>
    public int OrderCount { get; set; }

    /// <summary>本班营业额</summary>
    public decimal AmountTotal { get; set; }

    /// <summary>本班现金收入</summary>
    public decimal CashAmount { get; set; }

    /// <summary>本班自定义收款金额</summary>
    public decimal CustomAmount { get; set; }

    public string Remark { get; set; } = string.Empty;

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
}

/// <summary>离线补传队列</summary>
public class OutboxItem
{
    public long Id { get; set; }

    /// <summary>幂等键（同 Order.ClientOrderNo）</summary>
    public string ClientOrderNo { get; set; } = string.Empty;

    /// <summary>接口路径，如 /api/v1/order/settle</summary>
    public string ApiPath { get; set; } = string.Empty;

    /// <summary>请求体 JSON</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public SyncStatus Status { get; set; } = SyncStatus.Pending;

    public int RetryCount { get; set; }

    public string LastError { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? SentAt { get; set; }
}
