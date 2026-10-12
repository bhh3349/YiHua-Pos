namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品列表 ViewModel：分类筛选 + 关键词搜索（含别名）+ 状态筛选。
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

    public long? SelectedCategoryId { get; set; }
    public string SearchKeyword { get; set; } = string.Empty;
    public ProductStatusFilter StatusFilter { get; set; } = ProductStatusFilter.All;

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>别名索引：别名 → 商品。</summary>
    private readonly Dictionary<string, List<ProductDto>> _aliasIndex = new();

    /// <summary>
    /// 沽清商品 ID 集合（内存跟踪）。
    /// Domain 缺 IsSoldOut 字段，此处过渡用内存集合；
    /// 待 Domain 补充后改为 product.IsSoldOut，同步走 outbox（见报告）。
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
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载商品失败：{ex.Message}，请检查网络后重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 注册商品的别名（由编辑页在保存时调用）。
    /// Domain 补 Aliases 字段后，此处改为从数据源直接加载。
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
            ProductStatusFilter.OnSale => query.Where(p => p.IsOnSale && !SoldOutIds.Contains(p.Id)),
            ProductStatusFilter.SoldOut => query.Where(p => SoldOutIds.Contains(p.Id)),
            _ => query,
        };

        var keyword = SearchKeyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.Name.ToLowerInvariant().Contains(keyword) ||
                p.Code.ToLowerInvariant().Contains(keyword) ||
                p.PrePinYin.ToLowerInvariant().Contains(keyword) ||
                MatchesAlias(p, keyword));
        }

        return query.OrderBy(p => p.Sort).ThenBy(p => p.Name).ToList();
    }

    private bool MatchesAlias(ProductDto product, string keyword)
        => _aliasIndex.TryGetValue(keyword, out var list) && list.Contains(product);

    /// <summary>是否沽清（可测试）。</summary>
    public bool IsSoldOut(long productId) => SoldOutIds.Contains(productId);

    public bool IsEmpty => !IsLoading && ErrorMessage == null && GetFiltered().Count == 0;
}

public enum ProductStatusFilter
{
    All,
    OnSale,
    SoldOut,
}
