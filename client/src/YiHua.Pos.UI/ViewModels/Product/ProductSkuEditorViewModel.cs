namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 规格组编辑器 ViewModel：维度/选项的增删改，实时预览组合数。
/// </summary>
public class ProductSkuEditorViewModel
{
    public List<SpecDimension> Dimensions { get; } = new();

    /// <summary>当前组合数估算。</summary>
    public long EstimatedCount => SpecCombinationBuilder.EstimateCount(Dimensions);

    /// <summary>是否超过阈值（显示警告）。</summary>
    public bool HasWarning => SpecCombinationBuilder.ExceedsThreshold(Dimensions);

    /// <summary>警告文本。</summary>
    public string WarningText => HasWarning
        ? $"组合数约 {EstimatedCount}，超过阈值 {SpecCombinationBuilder.CombinationWarningThreshold}，请精简维度/选项"
        : string.Empty;

    /// <summary>校验问题。</summary>
    public List<string> Issues => SpecCombinationBuilder.Validate(Dimensions);

    public SpecDimension AddDimension(string name)
    {
        var dim = new SpecDimension { Name = name };
        Dimensions.Add(dim);
        return dim;
    }

    public void RemoveDimension(SpecDimension dim) => Dimensions.Remove(dim);

    public SpecOption AddOption(SpecDimension dim, string name, decimal priceAdjustment = 0m)
    {
        var opt = new SpecOption { Name = name, PriceAdjustment = priceAdjustment };
        dim.Options.Add(opt);
        return opt;
    }

    public void RemoveOption(SpecDimension dim, SpecOption opt) => dim.Options.Remove(opt);

    /// <summary>
    /// 预览组合（下单时显示的样子），取前 N 个。
    /// </summary>
    public List<SpecCombination> Preview(int maxCount = 10)
        => SpecCombinationBuilder.Build(Dimensions).Take(maxCount).ToList();
}
