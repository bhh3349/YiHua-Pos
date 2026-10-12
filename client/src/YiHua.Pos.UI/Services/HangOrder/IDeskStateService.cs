using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Services.HangOrder;

/// <summary>
/// 桌台状态协调接口。
/// 核心约束：桌台状态（Idle/Occupied）与挂单记录是两个独立状态。
/// 挂单后桌台变 Idle 可开新单，但挂单记录独立保留。
/// </summary>
public interface IDeskStateService
{
    /// <summary>挂单：桌台变空闲（可开新单），返回是否成功</summary>
    Task<bool> ParkDeskAsync(long deskId, CancellationToken ct = default);

    /// <summary>取单：桌台变占用，绑定订单</summary>
    Task<bool> OccupyDeskAsync(long deskId, long orderId, CancellationToken ct = default);

    /// <summary>获取桌台当前状态</summary>
    Task<Desk?> GetDeskAsync(long deskId, CancellationToken ct = default);

    /// <summary>桌台是否有挂单记录（独立于桌台状态）</summary>
    Task<bool> HasParkedOrdersAsync(long deskId, CancellationToken ct = default);
}
