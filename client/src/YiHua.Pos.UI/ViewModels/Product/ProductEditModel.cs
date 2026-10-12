namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品编辑模型（UI 层 DTO）。
/// Domain.Product 表达不了别名/多维度规格/套餐明细/沽清/时价，
/// 此处用完整模型表达，缺口见报告。
/// </summary>
public class ProductEditModel
{
    public long Id { get; set; }

    /// <summary>商品名称（必填）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>别名（多个，用于快速搜索，如"牛肉面"→"兰州牛肉面(小)"）。Domain 缺此字段。</summary>
    public List<string> Aliases { get; set; } = new();

    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public long UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;

    /// <summary>基础价（无规格时直接用；有时价时为参考价）。</summary>
    public decimal Price { get; set; }

    /// <summary>是否称重商品。Domain 有 WeightWay。</summary>
    public bool IsWeighted { get; set; }

    /// <summary>是否套餐。Domain 有 ProductType=2。</summary>
    public bool IsBundle { get; set; }

    /// <summary>是否时价（每日确认价格）。Domain 缺此字段。</summary>
    public bool IsMarketPrice { get; set; }

    /// <summary>是否沽清（可逆）。Domain 缺此字段（DataStatus 只有上架/下架）。</summary>
    public bool IsSoldOut { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>规格维度（多维度）。Domain 只有 SpecGroupName 单字符串，缺此结构。</summary>
    public List<SpecDimension> SpecDimensions { get; set; } = new();

    /// <summary>套餐明细。Domain 缺此结构。</summary>
    public List<BundleItem> BundleItems { get; set; } = new();

    /// <summary>套餐售价（可与单品总价不同）。</summary>
    public decimal BundlePrice { get; set; }

    public bool HasSpecs => SpecDimensions.Any(d => d.Options.Count > 0);
}

/// <summary>
/// 套餐明细项：从已有商品里选，数量固定，沽清联动。
/// </summary>
public class BundleItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string UnitName { get; set; } = string.Empty;

    /// <summary>该商品是否沽清（联动）。</summary>
    public bool IsSoldOut { get; set; }

    public decimal Subtotal => Quantity * UnitPrice;
}

/// <summary>
/// 商品编辑服务（UI 层契约，由 App/服务端同步层实现）。
/// IProductRepository 只有查询和 ReplaceAll（同步用），缺 Save/Update/Delete。
/// </summary>
public interface IProductEditService
{
    Task SaveAsync(ProductEditModel model, CancellationToken ct = default);
    Task DeleteAsync(long productId, CancellationToken ct = default);
    Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default);
}
