namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品列表 ViewModel：分类筛选 + 关键词搜索（名称/别名/拼音/编码）+ 状态筛选 + 批量操作。
/// 数据经 IProductDataSource 获取（App 层用 IProductRepository 实现）。
/// </summary>
public class ProductListViewModel
{
    private readonly IProductDataSource _dataSource;

    public ProductListViewModel(IProductDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public List<ProductDto> AllProducts { get; private set; } = new();
    public List<CategoryDto> Categories { get; private set; } = new();
    public List<UnitDto> Units { get; private set; } = new();

    public long? SelectedCategoryId { get; set; }
    public string SearchKeyword { get; set; } = string.Empty;
    public ProductStatusFilter StatusFilter { get; set; } = ProductStatusFilter.All;

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>别名索引：别名 → 商品（由编辑页保存时注册）。</summary>
    private readonly Dictionary<string, List<ProductDto>> _aliasIndex = new();

    /// <summary>
    /// 沽清商品 ID 镜像。
    /// 权威数据在商品表（Product.IsSoldOut），这里只是界面筛选用的快照，
    /// LoadAsync 时重新填充，不承担持久化职责。
    /// </summary>
    public HashSet<long> SoldOutIds { get; } = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            AllProducts = await _dataSource.GetAllAsync(ct);
            Categories = await _dataSource.GetCategoriesAsync(ct);
            Units = await _dataSource.GetUnitsAsync(ct);

            SoldOutIds.Clear();
            foreach (var p in AllProducts.Where(p => p.IsSoldOut))
                SoldOutIds.Add(p.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载商品失败：{ex.Message}，请检查后重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 注册商品的别名（由编辑页在保存时调用）。
    /// 商品表已有 Alias 字段，这里额外维护索引是为了「同一别名匹配多个商品」。
    /// </summary>
    public void RegisterAliases(long productId, IEnumerable<string> aliases)
    {
        var product = AllProducts.FirstOrDefault(p => p.Id == productId);
        if (product == null) return;
        foreach (var alias in aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            var key = alias.Trim().ToLowerInvariant();
            if (!_aliasIndex.TryGetValue(key, out var list))
            {
                list = new List<ProductDto>();
                _aliasIndex[key] = list;
            }
            if (!list.Contains(product))
                list.Add(product);
        }
    }

    /// <summary>
    /// 筛选商品（可测试的纯逻辑）。
    /// </summary>
    public List<ProductDto> GetFiltered()
    {
        IEnumerable<ProductDto> query = AllProducts;

        if (SelectedCategoryId.HasValue)
            query = query.Where(p => p.CategoryId == SelectedCategoryId.Value);

        query = StatusFilter switch
        {
            ProductStatusFilter.OnSale => query.Where(p => p.IsOnSale && !IsSoldOut(p.Id)),
            ProductStatusFilter.SoldOut => query.Where(p => IsSoldOut(p.Id)),
            ProductStatusFilter.OffShelf => query.Where(p => !p.IsOnSale),
            _ => query,
        };

        var keyword = SearchKeyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.Name.ToLowerInvariant().Contains(keyword) ||
                p.Code.ToLowerInvariant().Contains(keyword) ||
                p.PrePinYin.ToLowerInvariant().Contains(keyword) ||
                p.Alias.ToLowerInvariant().Contains(keyword) ||
                MatchesAlias(p, keyword));
        }

        return query.OrderBy(p => p.Sort).ThenBy(p => p.Name).ToList();
    }

    private bool MatchesAlias(ProductDto product, string keyword)
        => _aliasIndex.TryGetValue(keyword, out var list) && list.Contains(product);

    /// <summary>是否沽清。</summary>
    public bool IsSoldOut(long productId)
    {
        var p = AllProducts.FirstOrDefault(x => x.Id == productId);
        return (p?.IsSoldOut ?? false) || SoldOutIds.Contains(productId);
    }

    /// <summary>
    /// 设置沽清。写库 + 刷新本地镜像，两边一起变。
    /// </summary>
    public async Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
    {
        await _dataSource.SetSoldOutAsync(productId, soldOut, ct);

        var p = AllProducts.FirstOrDefault(x => x.Id == productId);
        if (p != null)
        {
            p.IsSoldOut = soldOut;
            p.SoldOutAt = soldOut ? DateTime.Now : null;
        }

        if (soldOut) SoldOutIds.Add(productId);
        else SoldOutIds.Remove(productId);
    }

    /// <summary>批量沽清 / 恢复。</summary>
    public async Task BatchSetSoldOutAsync(IEnumerable<long> ids, bool soldOut, CancellationToken ct = default)
    {
        foreach (var id in ids.ToList())
            await SetSoldOutAsync(id, soldOut, ct);
    }

    /// <summary>批量改价：给 newPrice 或 rate 之一（rate=0.9 表示九折）。</summary>
    public async Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
    {
        var affected = await _dataSource.BatchUpdatePriceAsync(ids, newPrice, rate, ct);
        await LoadAsync(ct);
        return affected;
    }

    /// <summary>批量上架 / 下架。</summary>
    public async Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, bool onSale, CancellationToken ct = default)
    {
        var affected = await _dataSource.BatchUpdateStatusAsync(ids, onSale ? 1 : 99, ct);
        await LoadAsync(ct);
        return affected;
    }

    public bool IsEmpty => !IsLoading && ErrorMessage == null && GetFiltered().Count == 0;
}

public enum ProductStatusFilter
{
    All,
    OnSale,
    SoldOut,

    /// <summary>已下架（老板主动停售，与沽清不同）。</summary>
    OffShelf,
}