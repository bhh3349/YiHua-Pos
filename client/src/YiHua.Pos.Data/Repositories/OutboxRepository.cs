using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>
/// 离线补传队列实现（T-003）。
///
/// 约定：
///   * 取出顺序按 CreatedAt 升序（Id 升序作为并列决胜），保证补传顺序与业务发生顺序一致；
///   * MarkFailedAsync 每次重试计数 +1，达到 <see cref="MaxRetries"/> 次后标记为 Failed，
///     不再参与补传（避免死循环占用队列）。
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    /// <summary>单条消息最大重试次数，超过后标记为 Failed。</summary>
    public const int MaxRetries = 10;

    private readonly ISqlSugarClient _db;

    public OutboxRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<OutboxItem>> GetPendingAsync(int limit = 100, CancellationToken ct = default)
        => _db.Queryable<OutboxItem>()
            .Where(o => o.Status == SyncStatus.Pending)
            .OrderBy(o => o.CreatedAt, OrderByType.Asc)
            .OrderBy(o => o.Id, OrderByType.Asc)
            .Take(limit)
            .ToListAsync();

    public async Task<long> EnqueueAsync(OutboxItem item, CancellationToken ct = default)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (item.CreatedAt == default)
            item.CreatedAt = EntityNormalizer.Now;
        EntityNormalizer.TruncateDates(item);
        var id = await _db.Insertable(item).ExecuteReturnIdentityAsync();
        item.Id = id;
        return id;
    }

    public Task MarkSucceededAsync(long id, CancellationToken ct = default)
        => _db.Updateable<OutboxItem>()
            .SetColumns(o => o.Status == SyncStatus.Succeeded)
            .SetColumns(o => o.SentAt == EntityNormalizer.Now)
            .Where(o => o.Id == id)
            .ExecuteCommandAsync();

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        var item = await _db.Queryable<OutboxItem>().Where(o => o.Id == id).FirstAsync();
        if (item == null)
            return;

        item.RetryCount++;
        item.LastError = error ?? string.Empty;
        if (item.RetryCount >= MaxRetries)
            item.Status = SyncStatus.Failed;

        await _db.Updateable(item).ExecuteCommandAsync();
    }

    public async Task<int> CountPendingAsync(CancellationToken ct = default)
        => await _db.Queryable<OutboxItem>()
            .Where(o => o.Status == SyncStatus.Pending)
            .CountAsync();
}

/// <summary>通用键值配置实现（T-003）。底层复用 Data 层内部的 DictEntry 表。</summary>
public sealed class DictionaryRepository : IDictionaryRepository
{
    private readonly ISqlSugarClient _db;

    public DictionaryRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var entry = await _db.Queryable<DictEntry>().Where(d => d.Key == key).FirstAsync();
        return entry?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("key 不能为空", nameof(key));

        var entry = await _db.Queryable<DictEntry>().Where(d => d.Key == key).FirstAsync();
        if (entry == null)
        {
            await _db.Insertable(new DictEntry
            {
                Key = key,
                Value = value ?? string.Empty,
                UpdatedAt = EntityNormalizer.Now
            }).ExecuteCommandAsync();
        }
        else
        {
            entry.Value = value ?? string.Empty;
            entry.UpdatedAt = EntityNormalizer.Now;
            await _db.Updateable(entry).ExecuteCommandAsync();
        }
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Queryable<DictEntry>().ToListAsync();
        return list.ToDictionary(d => d.Key, d => d.Value);
    }
}
