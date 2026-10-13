namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品编辑 ViewModel：分段表单（基础/规格/套餐），必填校验，保存按钮固定。
/// </summary>
public class ProductEditViewModel
{
    private readonly IProductEditService _editService;
    private readonly IProductDataSource? _dataSource;

    public ProductEditViewModel(IProductEditService editService, IProductDataSource? dataSource = null)
    {
        _editService = editService;
        _dataSource = dataSource;
    }

    public ProductEditModel Model { get; private set; } = new();

    /// <summary>可选分类（下拉框数据源）。</summary>
    public List<CategoryDto> Categories { get; private set; } = new();

    /// <summary>可选单位（下拉框数据源）。</summary>
    public List<UnitDto> Units { get; private set; } = new();

    /// <summary>当前编辑分段。</summary>
    public EditSection CurrentSection { get; set; } = EditSection.Basic;

    public bool IsSaving { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>是不是新建（界面标题用）。</summary>
    public bool IsNew => Model.Id <= 0;

    /// <summary>
    /// 载入商品进行编辑（新建时传 null）。
    /// 同时把分类/单位下拉的数据拉好 —— 空下拉框会让商家以为软件坏了。
    /// </summary>
    public async Task LoadAsync(ProductDto? product, CancellationToken ct = default)
    {
        if (_dataSource != null)
        {
            try
            {
                Categories = await _dataSource.GetCategoriesAsync(ct).ConfigureAwait(false);
                Units = await _dataSource.GetUnitsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载分类/单位失败：{ex.Message}";
            }
        }

        LoadForEdit(product);
    }

    /// <summary>载入商品进行编辑（同步版，测试用）。</summary>
    public void LoadForEdit(ProductDto? product)
    {
        if (product == null)
        {
            Model = new ProductEditModel
            {
                // 新建时给一个合理默认：第一个分类 + 第一个单位，减少必填项来回选
                CategoryId = Categories.FirstOrDefault()?.Id ?? 0,
                CategoryName = Categories.FirstOrDefault()?.Name ?? string.Empty,
                UnitId = Units.FirstOrDefault()?.Id ?? 0,
                UnitName = Units.FirstOrDefault()?.Name ?? string.Empty,
            };
        }
        else
        {
            Model = new ProductEditModel
            {
                Id = product.Id,
                Name = product.Name,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName,
                UnitId = product.UnitId,
                UnitName = product.UnitName,
                Price = product.Price,
                IsWeighted = product.IsWeighted,
                IsBundle = product.IsBundle,
                IsMarketPrice = product.IsMarketPrice,
                IsSoldOut = product.IsSoldOut,
                ImageUrl = product.ImageUrl,
                Aliases = string.IsNullOrWhiteSpace(product.Alias)
                    ? new List<string>()
                    : product.Alias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            };
        }
        ErrorMessage = null;
    }

    /// <summary>
    /// 校验（可测试的纯逻辑）。返回错误列表，空表示通过。
    /// 必填项给明确提示，不静默拒绝。
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Model.Name))
            errors.Add("商品名称不能为空");

        if (Model.CategoryId <= 0)
            errors.Add("请选择商品分类");

        if (Model.UnitId <= 0)
            errors.Add("请选择计量单位（份/例/杯/两/斤/串）");

        if (Model.Price < 0)
            errors.Add("价格不能为负数");

        if (Model.IsBundle)
        {
            if (Model.BundleItems.Count == 0)
                errors.Add("套餐至少要包含一个商品");
            if (Model.BundlePrice <= 0)
                errors.Add("套餐售价必须大于 0");
        }

        if (Model.HasSpecs)
        {
            var specIssues = SpecCombinationBuilder.Validate(Model.SpecDimensions);
            errors.AddRange(specIssues);
        }

        // 别名格式校验
        foreach (var alias in Model.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            if (alias.Length > 20)
                errors.Add($"别名「{alias}」过长（最多 20 字）");
        }

        return errors;
    }

    /// <summary>是否可保存（校验通过）。</summary>
    public bool CanSave => Validate().Count == 0;

    public async Task<(bool Success, List<string> Errors)> SaveAsync(CancellationToken ct = default)
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join("；", errors);
            return (false, errors);
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await _editService.SaveAsync(Model, ct);
            return (true, new List<string>());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败：{ex.Message}，请重试";
            return (false, new List<string> { ErrorMessage });
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>删除商品。</summary>
    public async Task<(bool Success, string? Error)> DeleteAsync(CancellationToken ct = default)
    {
        if (Model.Id <= 0)
            return (false, "新商品还没保存，不用删除。");

        try
        {
            await _editService.DeleteAsync(Model.Id, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            var message = $"删除失败：{ex.Message}";
            ErrorMessage = message;
            return (false, message);
        }
    }

    /// <summary>
    /// 删除前检查：有历史订单的商品建议停用而非删除。
    /// 返回提示文本，由 UI 做二次确认。
    /// </summary>
    public static string GetDeleteWarning(bool hasOrderHistory) => hasOrderHistory
        ? "该商品有历史订单，建议停用而非删除。删除后历史小票仍可查看，但无法恢复商品。确定删除吗？"
        : "确定删除该商品吗？删除后无法恢复。";
}

public enum EditSection
{
    Basic,
    Specs,
    Bundle,
}