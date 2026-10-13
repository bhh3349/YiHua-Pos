namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 分类管理 ViewModel。
/// 分类是商品的地基：没有分类就加不了菜，所以这个页面必须能真正增删改（不只是内存里改）。
/// </summary>
public class CategoryManageViewModel
{
    private readonly IProductDataSource? _dataSource;
    private readonly IProductEditService? _editService;

    public CategoryManageViewModel()
    {
    }

    public CategoryManageViewModel(IProductDataSource dataSource, IProductEditService editService)
    {
        _dataSource = dataSource;
        _editService = editService;
    }

    public List<CategoryDto> Categories { get; private set; } = new();

    /// <summary>每个分类下有多少商品（删除前提示用）。</summary>
    public Dictionary<long, int> ProductCounts { get; private set; } = new();

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? InfoMessage { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_dataSource == null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Categories = await _dataSource.GetCategoriesAsync(ct);

            var products = await _dataSource.GetAllAsync(ct);
            ProductCounts = products
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载分类失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>新增分类。名称为空或重复时明确报错。</summary>
    public async Task<(bool Ok, string? Error)> AddAsync(string name, CancellationToken ct = default)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return (false, "请输入分类名称");

        if (Categories.Any(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, $"已经有「{trimmed}」这个分类了");

        if (_editService == null)
            return (false, "分类服务未接入");

        try
        {
            var sort = Categories.Count == 0 ? 1 : Categories.Max(c => c.Sort) + 1;
            await _editService.SaveCategoryAsync(0, trimmed, sort, ct);
            await LoadAsync(ct);
            InfoMessage = $"已新增分类「{trimmed}」";
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"新增失败：{ex.Message}");
        }
    }

    /// <summary>重命名分类。</summary>
    public async Task<(bool Ok, string? Error)> RenameAsync(long id, string newName, CancellationToken ct = default)
    {
        var trimmed = (newName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return (false, "分类名称不能为空");

        var current = Categories.FirstOrDefault(c => c.Id == id);
        if (current == null)
            return (false, "分类不存在，请刷新后重试");

        if (Categories.Any(c => c.Id != id && string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, $"已经有「{trimmed}」这个分类了");

        if (_editService == null)
            return (false, "分类服务未接入");

        try
        {
            await _editService.SaveCategoryAsync(id, trimmed, current.Sort, ct);
            await LoadAsync(ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"重命名失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 删除分类。分类下还有商品时**拒绝**并说明原因 ——
    /// 直接删会让那些商品的分类变成空，老板在点单页找不到自己的菜。
    /// </summary>
    public async Task<(bool Ok, string? Error)> DeleteAsync(long id, CancellationToken ct = default)
    {
        var current = Categories.FirstOrDefault(c => c.Id == id);
        if (current == null)
            return (false, "分类不存在，请刷新后重试");

        var reason = CanDelete(current, ProductCounts.TryGetValue(id, out var n) ? n : 0);
        if (reason != null)
            return (false, reason);

        if (_editService == null)
            return (false, "分类服务未接入");

        try
        {
            await _editService.DeleteCategoryAsync(id, ct);
            await LoadAsync(ct);
            InfoMessage = $"已删除分类「{current.Name}」";
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"删除失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 删除前检查（可测试的纯逻辑）。返回 null 表示可删除，否则返回拒绝原因。
    /// </summary>
    public static string? CanDelete(CategoryDto category, int productCount)
        => productCount > 0
            ? $"分类「{category.Name}」下有 {productCount} 个商品，不能删除。请先移走商品或隐藏分类。"
            : null;

    /// <summary>删除前检查（按商品列表算，保留给旧调用方）。</summary>
    public static string? CanDelete(CategoryDto category, IEnumerable<ProductDto> products)
        => CanDelete(category, products.Count(p => p.CategoryId == category.Id));

    /// <summary>隐藏分类（前台不显示但保留历史数据）。</summary>
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

    /// <summary>排序（触控拖拽后调用，可测试）。</summary>
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