namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 套餐编辑器 ViewModel：从已有商品里选，售价独立，沽清联动。
/// </summary>
public class ProductBundleEditorViewModel
{
    /// <summary>套餐明细。</summary>
    public List<BundleItem> Items { get; } = new();

    /// <summary>套餐售价（可与单品总价不同）。</summary>
    public decimal BundlePrice { get; set; }

    /// <summary>单品总价（Σ 数量×单价）。</summary>
    public decimal ItemsTotal => Items.Sum(i => i.Subtotal);

    /// <summary>优惠金额（单品总价 - 套餐价，可为负表示加价）。</summary>
    public decimal DiscountAmount => ItemsTotal - BundlePrice;

    /// <summary>
    /// 添加商品到套餐（可测试）。
    /// 套餐里的商品从库，数量固定为 1（不可改）。
    /// </summary>
    public void AddItem(ProductDto product)
    {
        if (Items.Any(i => i.ProductId == product.Id))
            return; // 已在套餐中，不重复添加

        Items.Add(new BundleItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = 1, // 固定，不可改
            UnitPrice = product.Price,
            UnitName = product.UnitName,
        });
    }

    public void RemoveItem(long productId)
    {
        var item = Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
            Items.Remove(item);
    }

    /// <summary>
    /// 沽清联动：套餐中任一商品沽清，套餐不可售（可测试）。
    /// </summary>
    public bool IsAvailable(IEnumerable<long> soldOutProductIds)
        => !Items.Any(i => soldOutProductIds.Contains(i.ProductId));

    /// <summary>
    /// 更新沽清状态（可测试）。
    /// </summary>
    public void UpdateSoldOutStatus(IEnumerable<long> soldOutProductIds)
    {
        var set = new HashSet<long>(soldOutProductIds);
        foreach (var item in Items)
            item.IsSoldOut = set.Contains(item.ProductId);
    }

    /// <summary>
    /// 校验（可测试）。
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Items.Count == 0)
            errors.Add("套餐至少要包含一个商品");
        if (BundlePrice <= 0)
            errors.Add("套餐售价必须大于 0");
        return errors;
    }
}
