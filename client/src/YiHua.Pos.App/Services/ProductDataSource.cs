using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 商品数据源适配器：把 Domain 的 IProductRepository 桥接到 UI 层的 IProductDataSource。
///
/// 存在的理由：YiHua.Pos.UI.csproj 冻结不能引用 Domain，
/// 所以 UI 层自己定了一套 DTO 契约，注释里写明「App 层用 IProductRepository 实现」。
/// 这个类就是那个 App 层实现。
/// </summary>
internal sealed class ProductDataSource : IProductDataSource
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitRepository _units;

    public ProductDataSource(
        IProductRepository products,
        ICategoryRepository categories,
        IUnitRepository units)
    {
        _products = products;
        _categories = categories;
        _units = units;
    }

    public async Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default)
        => (await _products.GetAllAsync(ct).ConfigureAwait(false)).Select(Map).ToList();

    public async Task<List<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
        => (await _products.GetByCategoryAsync(categoryId, ct).ConfigureAwait(false)).Select(Map).ToList();

    public async Task<List<ProductDto>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
        => (await _products.SearchAsync(keyword, limit, ct).ConfigureAwait(false)).Select(Map).ToList();

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
        => (await _categories.GetAllAsync(ct).ConfigureAwait(false))
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Sort = c.Sort,
                DataStatus = c.DataStatus,
            }).ToList();

    public async Task<List<UnitDto>> GetUnitsAsync(CancellationToken ct = default)
        => (await _units.GetAllAsync(ct).ConfigureAwait(false))
            .Select(u => new UnitDto { Id = u.Id, Name = u.Name }).ToList();

    public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
        => _products.SetSoldOutAsync(productId, soldOut, null, ct);

    public Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
        => _products.BatchUpdatePriceAsync(ids, newPrice, rate, ct);

    public Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default)
        => _products.BatchUpdateStatusAsync(ids, dataStatus, ct);

    /// <summary>
    /// Domain.Product → ProductDto。字段一一对应，改一边必须改另一边。
    /// </summary>
    internal static ProductDto Map(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Code = p.Code,
        Barcode = p.Barcode,
        CategoryId = p.CategoryId,
        CategoryName = p.CategoryName,
        UnitId = p.UnitId,
        UnitName = p.UnitName,
        Price = p.Price,
        ProductType = p.ProductType,
        WeightWay = p.WeightWay,
        PrePinYin = p.PrePinYin,
        ImageUrl = p.ImageUrl,
        DataStatus = p.DataStatus,
        Sort = p.Sort,
        IsSoldOut = p.IsSoldOut,
        SoldOutAt = p.SoldOutAt,
        IsMarketPrice = p.IsMarketPrice,
        Alias = p.Alias,
    };
}