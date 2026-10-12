namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 分类管理 ViewModel：新建/重命名/排序/隐藏，删除前检查关联商品。
/// </summary>
public class CategoryManageViewModel
{
    public List<CategoryDto> Categories { get; private set; } = new();
    public string? ErrorMessage { get; private set; }

    public void Load(IEnumerable<CategoryDto> categories)
    {
        Categories = categories.OrderBy(c => c.Sort).ToList();
    }

    /// <summary>
    /// 删除分类前检查（可测试的纯逻辑）。
    /// 返回 null 表示可删除，否则返回拒绝原因。
    /// </summary>
    public static string? CanDelete(CategoryDto category, IEnumerable<ProductDto> products)
    {
        var count = products.Count(p => p.CategoryId == category.Id);
        if (count > 0)
            return $"分类「{category.Name}」下有 {count} 个商品，不能删除。请先移走商品或隐藏分类。";
        return null;
    }

    /// <summary>
    /// 隐藏分类（前台不显示但保留历史数据）。
    /// </summary>
    public void HideCategory(long categoryId)
    {
        var cat = Categories.FirstOrDefault(c => c.Id == categoryId);
        if (cat != null)
            cat.DataStatus = 99;
    }

    public void ShowCategory(long categoryId)
    {
        var cat = Categories.FirstOrDefault(c => c.Id == categoryId);
        if (cat != null)
            cat.DataStatus = 1;
    }

    /// <summary>
    /// 排序（触控拖拽后调用，可测试）。
    /// </summary>
    public void Reorder(IList<long> orderedIds)
    {
        var map = Categories.ToDictionary(c => c.Id);
        var reordered = new List<CategoryDto>();
        int sort = 0;
        foreach (var id in orderedIds)
        {
            if (map.TryGetValue(id, out var cat))
            {
                cat.Sort = sort++;
                reordered.Add(cat);
            }
        }
        foreach (var cat in Categories.Where(c => !orderedIds.Contains(c.Id)))
        {
            cat.Sort = sort++;
            reordered.Add(cat);
        }
        Categories = reordered;
    }
}
