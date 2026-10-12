using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>
/// 挂单记录。
///
/// 关键设计：桌台状态（Desk.Status）与挂单记录是**两个独立状态**。
/// 挂单后桌台变 Idle 可以开新单，但本记录独立存在不丢失。
///
/// 如果把「桌台有挂单」直接编码进 Desk.Status（比如加一个 HasHangOrder 位），
/// 就会出现两个必然的 bug：
///   ① 桌台开新单后挂单标记被清掉 → 挂单丢失
///   ② 桌台被占用时无法显示「原来有个挂单」
/// 所以这里坚持两个状态分开存。
/// </summary>
public class HangOrderRecord
{
    /// <summary>挂单 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>原桌台 ID</summary>
    public long DeskId { get; set; }

    /// <summary>原桌号（大而醒目显示用）</summary>
    public string DeskName { get; set; } = string.Empty;

    /// <summary>原订单 ID</summary>
    public long OrderId { get; set; }

    public int GuestCount { get; set; }

    /// <summary>挂单时订单应付金额</summary>
    public decimal Amount { get; set; }

    public DateTime ParkTime { get; set; } = DateTime.Now;

    public string OperatorName { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;

    /// <summary>已预结账的明细 ID（多次预结账累加）</summary>
    public List<long> SettledItemIds { get; set; } = new();

    /// <summary>
    /// 挂单状态。
    /// </summary>
    public HangOrderStatus Status { get; set; } = HangOrderStatus.Parked;
}