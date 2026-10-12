using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>商品仓储实现（T-003）。读多写少；同步场景用 ReplaceAllAsync 整体替换。</summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly ISqlSugarClient _db;

    public ProductRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default)
        => _db.Queryable<Product>()
            .OrderBy(p => p.Sort, OrderByType.Asc)
            .OrderBy(p => p.Id, OrderByType.Asc)
            .ToListAsync();

    public Task<List<Product>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
        => _db.Queryable<Product>()
            .Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.Sort, OrderByType.Asc)
            .OrderBy(p => p.Id, OrderByType.Asc)
            .ToListAsync();

    public async Task<Product?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.Queryable<Product>().Where(p => p.Id == id).FirstAsync();

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => await _db.Queryable<Product>().Where(p => p.Barcode == barcode).FirstAsync();

    /// <summary>按名称 / 名称首字母(拼音) / 商品编码模糊搜索，空关键字直接返回空集合。</summary>
    public Task<List<Product>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Task.FromResult(new List<Product>());

        keyword = keyword.Trim();
        return _db.Queryable<Product>()
            .Where(p => p.Name.Contains(keyword)
                     || p.PrePinYin.Contains(keyword)
                     || p.Code.Contains(keyword))
            .OrderBy(p => p.Sort, OrderByType.Asc)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 批量替换（服务端同步用）：先删后插，同一事务内完成，避免出现中间态。
    /// 注意：订单明细里冗余了下单时的商品快照，不受替换影响。
    /// </summary>
    public async Task ReplaceAllAsync(IEnumerable<Product> products, CancellationToken ct = default)
    {
        var list = products?.ToList() ?? new List<Product>();
        await Tran.RunAsync(_db, async () =>
        {
            await _db.Deleteable<Product>().ExecuteCommandAsync();
            if (list.Count > 0)
                await _db.Insertable(list).ExecuteCommandAsync();
        });
    }
}

/// <summary>分类仓储实现（T-003）。</summary>
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly ISqlSugarClient _db;

    public CategoryRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<ProductCategory>> GetAllAsync(CancellationToken ct = default)
        => _db.Queryable<ProductCategory>()
            .OrderBy(c => c.Sort, OrderByType.Asc)
            .OrderBy(c => c.Id, OrderByType.Asc)
            .ToListAsync();

    public async Task ReplaceAllAsync(IEnumerable<ProductCategory> categories, CancellationToken ct = default)
    {
        var list = categories?.ToList() ?? new List<ProductCategory>();
        await Tran.RunAsync(_db, async () =>
        {
            await _db.Deleteable<ProductCategory>().ExecuteCommandAsync();
            if (list.Count > 0)
                await _db.Insertable(list).ExecuteCommandAsync();
        });
    }
}

/// <summary>单位仓储实现（T-003）。</summary>
public sealed class UnitRepository : IUnitRepository
{
    private readonly ISqlSugarClient _db;

    public UnitRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<ProductUnit>> GetAllAsync(CancellationToken ct = default)
        => _db.Queryable<ProductUnit>()
            .OrderBy(u => u.Sort, OrderByType.Asc)
            .OrderBy(u => u.Id, OrderByType.Asc)
            .ToListAsync();

    public async Task ReplaceAllAsync(IEnumerable<ProductUnit> units, CancellationToken ct = default)
    {
        var list = units?.ToList() ?? new List<ProductUnit>();
        await Tran.RunAsync(_db, async () =>
        {
            await _db.Deleteable<ProductUnit>().ExecuteCommandAsync();
            if (list.Count > 0)
                await _db.Insertable(list).ExecuteCommandAsync();
        });
    }
}
