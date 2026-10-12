namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 商品编辑 ViewModel：分段表单（基础/规格/套餐），必填校验，保存按钮固定。
/// </summary>
public class ProductEditViewModel
{
    private readonly IProductEditService _editService;

    public ProductEditViewModel(IProductEditService editService)
    {
        _editService = editService;
    }

    public ProductEditModel Model { get; private set; } = new();

    /// <summary>当前编辑分段。</summary>
    public EditSection CurrentSection { get; set; } = EditSection.Basic;

    public bool IsSaving { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>载入商品进行编辑（新建时传 null）。</summary>
    public void LoadForEdit(ProductDto? product)
    {
        if (product == null)
        {
            Model = new ProductEditModel();
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
                ImageUrl = product.ImageUrl,
                // 别名/规格/套餐明细/沽清/时价：Domain 缺字段，新建时为空，编辑时无法回填（见报告）
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

        if (!Model.IsMarketPrice && Model.Price < 0)
            errors.Add("价格不能为负数");

        if (Model.IsMarketPrice && Model.Price < 0)
            errors.Add("时价参考价不能为负数");

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
            return (false, errors);

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
