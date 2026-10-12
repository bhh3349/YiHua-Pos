using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
namespace YiHua.Pos.UI.Services.HangOrder;

/// <summary>挂单仓储内存实现（测试/演示用，线程安全）</summary>
public sealed class InMemoryHangOrderRepository : IHangOrderRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HangOrderRecord> _records = new();

    public Task<HangOrderRecord> ParkAsync(HangOrderRecord record, CancellationToken ct = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        lock (_lock)
        {
            if (string.IsNullOrEmpty(record.Id))
                record.Id = Guid.NewGuid().ToString("N");
            _records[record.Id] = record;
            return Task.FromResult(record);
        }
    }

    public Task<HangOrderRecord?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records.TryGetValue(id, out var r);
            return Task.FromResult(r);
        }
    }

    /// <summary>
    /// 默认只返回 Parked（待取单）。
    /// includeClosed = true 时连同已取回/已作废的一并返回，供历史查询用。
    /// </summary>
    public Task<List<HangOrderRecord>> GetAllAsync(bool includeClosed = false, CancellationToken ct = default)
    {
        lock (_lock)
        {
            // 按挂单时间倒序
            return Task.FromResult(_records.Values
                .Where(r => includeClosed || r.Status == HangOrderStatus.Parked)
                .OrderByDescending(r => r.ParkTime)
                .ToList());
        }
    }

    public Task<List<HangOrderRecord>> GetByDeskIdAsync(long deskId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_records.Values
                .Where(r => r.DeskId == deskId)
                .OrderByDescending(r => r.ParkTime)
                .ToList());
        }
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records.Remove(id);
            return Task.CompletedTask;
        }
    }

    public Task UpdateAsync(HangOrderRecord record, CancellationToken ct = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        lock (_lock)
        {
            _records[record.Id] = record;
            return Task.CompletedTask;
        }
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_records.Count);
    }
}
