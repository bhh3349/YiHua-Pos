using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>商品仓储实现（T-003）。读多写少。</summary>
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

    /// <summary>按名称 / 名称首字母(拼音) / 商品编码 / 别名模糊搜索，空关键字直接返回空集合。</summary>
    public Task<List<Product>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Task.FromResult(new List<Product>());

        keyword = keyword.Trim();
        return _db.Queryable<Product>()
            .Where(p => p.Name.Contains(keyword)
                     || p.PrePinYin.Contains(keyword)
                     || p.Code.Contains(keyword)
                     || p.Alias.Contains(keyword))
            .OrderBy(p => p.Sort, OrderByType.Asc)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>新增或更新单个商品（Id=0 视为新增）。</summary>
    public async Task<long> SaveAsync(Product product, CancellationToken ct = default)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (string.IsNullOrWhiteSpace(product.Name))
            throw new ArgumentException("商品名称不能为空", nameof(product));

        EntityNormalizer.TruncateDates(product);

        if (product.Id <= 0)
        {
            var id = await _db.Insertable(product).ExecuteReturnIdentityAsync();
            product.Id = id;
            return id;
        }

        await _db.Updateable(product).ExecuteCommandAsync();
        return product.Id;
    }

    public Task DeleteAsync(long id, CancellationToken ct = default)
        => _db.Deleteable<Product>().Where(p => p.Id == id).ExecuteCommandAsync();

    /// <summary>
    /// 沽清：只动 IsSoldOut，不动 DataStatus。
    /// 两者语义不同 —— 沽清是「今天卖完了」（第二天要自动恢复），
    /// 下架是老板主动停售。混用会导致沽清的菜永远不回来。
    /// </summary>
    public Task SetSoldOutAsync(long id, bool soldOut, DateTime? at = null, CancellationToken ct = default)
        => _db.Updateable<Product>()
            .SetColumns(p => p.IsSoldOut == soldOut)
            .SetColumns(p => p.SoldOutAt == (soldOut ? (at ?? DateTime.Now) : null))
            .Where(p => p.Id == id)
            .ExecuteCommandAsync();

    /// <summary>批量改价：newPrice 优先；否则按 rate 打折（rate=0.9 表示九折）。</summary>
    public async Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
    {
        var idList = ids?.ToList() ?? new List<long>();
        if (idList.Count == 0) return 0;
        if (newPrice == null && rate == null)
            throw new ArgumentException("必须给出新价格或折扣率");

        var affected = 0;
        foreach (var id in idList)
        {
            var p = await _db.Queryable<Product>().Where(x => x.Id == id).FirstAsync();
            if (p == null) continue;

            var target = newPrice ?? Math.Round(p.Price * rate!.Value, 2, MidpointRounding.AwayFromZero);
            if (target < 0) target = 0;

            p.Price = target;
            await _db.Updateable(p).ExecuteCommandAsync();
            affected++;
        }
        return affected;
    }

    /// <summary>批量改上下架状态（1=上架 99=下架）。</summary>
    public async Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default)
    {
        var idList = ids?.ToList() ?? new List<long>();
        if (idList.Count == 0) return 0;

        return await _db.Updateable<Product>()
            .SetColumns(p => p.DataStatus == dataStatus)
            .Where(p => idList.Contains(p.Id))
            .ExecuteCommandAsync();
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

    public async Task<long> SaveAsync(ProductCategory category, CancellationToken ct = default)
    {
        if (category == null) throw new ArgumentNullException(nameof(category));
        if (string.IsNullOrWhiteSpace(category.Name))
            throw new ArgumentException("分类名称不能为空", nameof(category));

        category.Name = category.Name.Trim();

        if (category.Id <= 0)
        {
            var id = await _db.Insertable(category).ExecuteReturnIdentityAsync();
            category.Id = id;
            return id;
        }

        await _db.Updateable(category).ExecuteCommandAsync();
        return category.Id;
    }

    /// <summary>
    /// 删除分类。**不级联删商品** —— 商品有历史订单，误删分类不该连带丢菜品。
    /// 调用方需要先判断该分类下是否还有商品。
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken ct = default)
        => _db.Deleteable<ProductCategory>().Where(c => c.Id == id).ExecuteCommandAsync();

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

    public async Task<long> SaveAsync(ProductUnit unit, CancellationToken ct = default)
    {
        if (unit == null) throw new ArgumentNullException(nameof(unit));
        if (string.IsNullOrWhiteSpace(unit.Name))
            throw new ArgumentException("单位名称不能为空", nameof(unit));

        unit.Name = unit.Name.Trim();

        if (unit.Id <= 0)
        {
            var id = await _db.Insertable(unit).ExecuteReturnIdentityAsync();
            unit.Id = id;
            return id;
        }

        await _db.Updateable(unit).ExecuteCommandAsync();
        return unit.Id;
    }

    public Task DeleteAsync(long id, CancellationToken ct = default)
        => _db.Deleteable<ProductUnit>().Where(u => u.Id == id).ExecuteCommandAsync();

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