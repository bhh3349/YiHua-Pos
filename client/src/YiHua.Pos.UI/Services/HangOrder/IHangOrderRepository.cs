using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.Services.HangOrder;

/// <summary>
/// 挂单记录。
/// 关键设计：桌台状态（Desk.Status）与挂单记录是两个独立状态。
/// 挂单后桌台变 Idle 可开新单，但本记录独立存在不丢失。
/// </summary>
public sealed class HangOrderRecord
{
    /// <summary>挂单ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>原桌台ID</summary>
    public long DeskId { get; set; }

    /// <summary>原桌号（大而醒目显示用）</summary>
    public string DeskName { get; set; } = string.Empty;

    /// <summary>原订单ID</summary>
    public long OrderId { get; set; }

    public int GuestCount { get; set; }

    /// <summary>挂单时订单应付金额</summary>
    public decimal Amount { get; set; }

    public DateTime ParkTime { get; set; } = DateTime.Now;

    /// <summary>挂单人</summary>
    public string OperatorName { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;

    /// <summary>已预结账的明细ID（多次预结账累加）</summary>
    public List<long> SettledItemIds { get; set; } = new();

    /// <summary>等待时长</summary>
    public TimeSpan WaitDuration => DateTime.Now - ParkTime;
}

/// <summary>挂单仓储接口（可注入，Domain 补实体后可换持久化实现）</summary>
public interface IHangOrderRepository
{
    Task<HangOrderRecord> ParkAsync(HangOrderRecord record, CancellationToken ct = default);
    Task<HangOrderRecord?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<HangOrderRecord>> GetAllAsync(CancellationToken ct = default);
    Task<List<HangOrderRecord>> GetByDeskIdAsync(long deskId, CancellationToken ct = default);
    Task RemoveAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(HangOrderRecord record, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
