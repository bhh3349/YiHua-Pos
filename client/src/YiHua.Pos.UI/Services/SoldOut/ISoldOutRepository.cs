namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>沽清仓储接口（Domain 补实体后可换持久化实现）</summary>
public interface ISoldOutRepository
{
    Task<SoldOutRecord?> GetByProductIdAsync(long productId, CancellationToken ct = default);
    Task<List<SoldOutRecord>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(SoldOutRecord record, CancellationToken ct = default);
    Task RemoveAsync(long productId, CancellationToken ct = default);
    Task<List<SoldOutRecord>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
