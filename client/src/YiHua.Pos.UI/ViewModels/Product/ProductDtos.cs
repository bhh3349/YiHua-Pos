namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品 DTO（UI 层本地数据形状）。
/// 因 YiHua.Pos.UI.csproj 冻结无法引用 Domain，
/// 由 App 层将 Domain.Product 映射为此 DTO（字段一一对应）。
/// </summary>
public class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int ProductType { get; set; } = 1; // 1=普通 2=套餐 3=称重
    public int WeightWay { get; set; }       // 0=不计重 1=计重
    public string PrePinYin { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DataStatus { get; set; } = 1;  // 1=上架 99=下架
    public int Sort { get; set; }

    /// <summary>沽清（今天卖完了，可逆；与下架不同）。</summary>
    public bool IsSoldOut { get; set; }

    /// <summary>沽清时间（定时自动还原按它判断是否隔天）。</summary>
    public DateTime? SoldOutAt { get; set; }

    /// <summary>时价：每天开市前确认当日价格。</summary>
    public bool IsMarketPrice { get; set; }

    /// <summary>别名，逗号分隔。</summary>
    public string Alias { get; set; } = string.Empty;

    public bool IsOnSale => DataStatus == 1;
    public bool IsWeighted => WeightWay == 1 || ProductType == 3;
    public bool IsBundle => ProductType == 2;

    /// <summary>可点：已上架且未沽清。</summary>
    public bool CanOrder => DataStatus == 1 && !IsSoldOut;
}

/// <summary>分类 DTO。</summary>
public class CategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Sort { get; set; }
    public int DataStatus { get; set; } = 1;
}

/// <summary>单位 DTO。</summary>
public class UnitDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 商品数据源（UI 层契约）。
/// App 层用 IProductRepository 实现此接口，向 ViewModel 提供数据。
/// </summary>
public interface IProductDataSource
{
    Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<UnitDto>> GetUnitsAsync(CancellationToken ct = default);

    /// <summary>按名称/别名/拼音/编码搜索。</summary>
    Task<List<ProductDto>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default);

    /// <summary>设置沽清（可逆）。</summary>
    Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default);

    /// <summary>批量改价：给 newPrice 或 rate 之一。</summary>
    Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default);

    /// <summary>批量改上下架状态。</summary>
    Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default);
}

/// <summary>沽清列表项。</summary>
public class SoldOutItemDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SoldOutTime { get; set; }
}
