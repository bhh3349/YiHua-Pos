namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>沽清仓储内存实现（测试用，线程安全）</summary>
public sealed class FakeSoldOutRepository : ISoldOutRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<long, SoldOutRecord> _records = new();

    public Task<SoldOutRecord?> GetByProductIdAsync(long productId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records.TryGetValue(productId, out var r);
            return Task.FromResult(r);
        }
    }

    public Task<List<SoldOutRecord>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_records.Values.ToList());
    }

    public Task SaveAsync(SoldOutRecord record, CancellationToken ct = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        lock (_lock)
        {
            _records[record.ProductId] = record;
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(long productId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records.Remove(productId);
            return Task.CompletedTask;
        }
    }

    public Task<List<SoldOutRecord>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_records.Values.Where(r => r.CategoryId == categoryId).ToList());
        }
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_records.Count);
    }
}
