namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 规格维度（如"温度"/"糖度"/"加料"）。
/// </summary>
public class SpecDimension
{
    /// <summary>维度名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>选项列表。</summary>
    public List<SpecOption> Options { get; set; } = new();

    /// <summary>
    /// 维度内是否可多选（如"加料"可多选珍珠+椰果；"温度"单选）。
    /// </summary>
    public bool AllowMultiSelect { get; set; }

    /// <summary>
    /// 是否参与跨维度组合。false 表示该维度独立成 SKU（如火锅锅底只选其一，不与其他维度相乘）。
    /// </summary>
    public bool IsCombinable { get; set; } = true;
}

/// <summary>
/// 规格选项（如"少冰"/"珍珠"）。
/// </summary>
public class SpecOption
{
    /// <summary>选项名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>价格调整（+2元 / -1元 / 0）。</summary>
    public decimal PriceAdjustment { get; set; }

    /// <summary>是否默认选中。</summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// 一个规格组合（如"大杯/少冰/七分糖/珍珠"）。
/// </summary>
public class SpecCombination
{
    /// <summary>单选维度的选择：维度名 → 选项名。</summary>
    public Dictionary<string, string> SingleSelections { get; } = new();

    /// <summary>多选维度的选择：维度名 → 选项名列表。</summary>
    public Dictionary<string, List<string>> MultiSelections { get; } = new();

    /// <summary>相对基础价的价格调整总和。</summary>
    public decimal PriceAdjustment { get; set; }

    /// <summary>组合描述（如"大杯 少冰 七分糖 加珍珠"）。</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>基于基础价计算实际售价。</summary>
    public decimal GetPrice(decimal basePrice) => basePrice + PriceAdjustment;
}

/// <summary>
/// 规格组合生成器（纯逻辑）。
/// 规则：
/// - IsCombinable=true 的维度做笛卡尔积
/// - IsCombinable=false 的维度每个选项独立成一个组合（不与其他维度相乘）
/// - AllowMultiSelect=true 的维度取幂集（可多选，含空选）
/// - 组合数超过阈值时警告（茶饮 3维×4选项 易爆炸）
/// </summary>
public static class SpecCombinationBuilder
{
    /// <summary>组合数警告阈值。</summary>
    public const int CombinationWarningThreshold = 100;

    /// <summary>
    /// 生成所有规格组合。
    /// </summary>
    /// <param name="dimensions">规格维度</param>
    /// <param name="basePrice">基础价（用于计算 DisplayText，不影响组合结构）</param>
    /// <returns>组合列表</returns>
    public static List<SpecCombination> Build(IList<SpecDimension> dimensions, decimal basePrice = 0m)
    {
        var result = new List<SpecCombination>();
        if (dimensions == null || dimensions.Count == 0)
            return result;

        // 过滤掉没有选项的维度
        var valid = dimensions.Where(d => d.Options != null && d.Options.Count > 0).ToList();
        if (valid.Count == 0)
            return result;

        var combinable = valid.Where(d => d.IsCombinable).ToList();
        var standalone = valid.Where(d => !d.IsCombinable).ToList();

        // 可组合维度：笛卡尔积（无可组合维度时不产生空组合）
        if (combinable.Count > 0)
            result.AddRange(BuildCartesian(combinable));

        // 独立维度：每个选项一个组合
        foreach (var dim in standalone)
        {
            foreach (var opt in dim.Options)
            {
                var combo = new SpecCombination();
                if (dim.AllowMultiSelect)
                    combo.MultiSelections[dim.Name] = new List<string> { opt.Name };
                else
                    combo.SingleSelections[dim.Name] = opt.Name;
                combo.PriceAdjustment = opt.PriceAdjustment;
                combo.DisplayText = BuildDisplayText(combo);
                result.Add(combo);
            }
        }

        // 合并：如果既有组合维度又有独立维度，独立维度附加到每个组合上？
        // 不——独立维度就是独立的 SKU，不与组合相乘。直接返回两者。

        foreach (var c in result)
            c.DisplayText = BuildDisplayText(c);

        return result;
    }

    private static List<SpecCombination> BuildCartesian(List<SpecDimension> dimensions)
    {
        var result = new List<SpecCombination> { new SpecCombination() };

        foreach (var dim in dimensions)
        {
            var next = new List<SpecCombination>();
            // 该维度的所有可能选择（含多选的幂集）
            var selections = GetDimensionSelections(dim);

            foreach (var existing in result)
            {
                foreach (var (single, multi, priceAdj) in selections)
                {
                    var combo = Clone(existing);
                    if (single != null)
                        combo.SingleSelections[dim.Name] = single;
                    if (multi != null)
                        combo.MultiSelections[dim.Name] = multi;
                    combo.PriceAdjustment += priceAdj;
                    next.Add(combo);
                }
            }
            result = next;
        }

        return result;
    }

    /// <summary>
    /// 获取一个维度的所有可能选择。
    /// 单选：每个选项一种；多选：幂集（含空选）。
    /// 返回：(单选选项名, 多选选项名列表, 价格调整)
    /// </summary>
    private static List<(string? Single, List<string>? Multi, decimal PriceAdj)> GetDimensionSelections(SpecDimension dim)
    {
        var selections = new List<(string?, List<string>?, decimal)>();

        if (dim.AllowMultiSelect)
        {
            // 幂集：含空选
            var options = dim.Options;
            int n = options.Count;
            for (int mask = 0; mask < (1 << n); mask++)
            {
                var chosen = new List<string>();
                decimal adj = 0;
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        chosen.Add(options[i].Name);
                        adj += options[i].PriceAdjustment;
                    }
                }
                selections.Add((null, chosen, adj));
            }
        }
        else
        {
            foreach (var opt in dim.Options)
                selections.Add((opt.Name, null, opt.PriceAdjustment));
        }

        return selections;
    }

    private static SpecCombination Clone(SpecCombination source)
    {
        var clone = new SpecCombination { PriceAdjustment = source.PriceAdjustment };
        foreach (var kv in source.SingleSelections)
            clone.SingleSelections[kv.Key] = kv.Value;
        foreach (var kv in source.MultiSelections)
            clone.MultiSelections[kv.Key] = new List<string>(kv.Value);
        return clone;
    }

    private static string BuildDisplayText(SpecCombination combo)
    {
        var parts = new List<string>();
        parts.AddRange(combo.SingleSelections.Values);
        foreach (var kv in combo.MultiSelections)
        {
            if (kv.Value.Count > 0)
                parts.Add("加" + string.Join("+", kv.Value));
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// 估算组合数（不实际生成，用于上限预警）。
    /// </summary>
    public static long EstimateCount(IList<SpecDimension> dimensions)
    {
        if (dimensions == null || dimensions.Count == 0)
            return 0;

        long total = 0;
        bool hasCombinable = false;
        long cartesian = 1;

        foreach (var dim in dimensions)
        {
            if (dim.Options == null || dim.Options.Count == 0)
                continue;

            long dimCount = dim.AllowMultiSelect
                ? (1L << dim.Options.Count) // 幂集
                : dim.Options.Count;

            if (dim.IsCombinable)
            {
                hasCombinable = true;
                cartesian *= dimCount;
            }
            else
            {
                total += dimCount; // 独立维度直接相加
            }
        }

        if (hasCombinable)
            total += cartesian;

        return total;
    }

    /// <summary>
    /// 组合数是否超过警告阈值。
    /// </summary>
    public static bool ExceedsThreshold(IList<SpecDimension> dimensions)
        => EstimateCount(dimensions) > CombinationWarningThreshold;

    /// <summary>
    /// 验证规格配置，返回问题列表（空表示合法）。
    /// </summary>
    public static List<string> Validate(IList<SpecDimension> dimensions)
    {
        var issues = new List<string>();
        if (dimensions == null || dimensions.Count == 0)
            return issues;

        var names = new HashSet<string>();
        foreach (var dim in dimensions)
        {
            if (string.IsNullOrWhiteSpace(dim.Name))
                issues.Add("规格维度名称不能为空");
            else if (!names.Add(dim.Name))
                issues.Add($"规格维度名称重复：{dim.Name}");

            if (dim.Options == null || dim.Options.Count == 0)
                issues.Add($"维度「{dim.Name}」没有选项");
            else
            {
                var optNames = new HashSet<string>();
                foreach (var opt in dim.Options)
                {
                    if (string.IsNullOrWhiteSpace(opt.Name))
                        issues.Add($"维度「{dim.Name}」有空选项名");
                    else if (!optNames.Add(opt.Name))
                        issues.Add($"维度「{dim.Name}」选项重复：{opt.Name}");
                }
            }

            if (dim.AllowMultiSelect && dim.Options != null && dim.Options.Count > 6)
                issues.Add($"维度「{dim.Name}」多选项过多（{dim.Options.Count}个），组合数会爆炸");
        }

        if (ExceedsThreshold(dimensions))
            issues.Add($"组合数约 {EstimateCount(dimensions)}，超过阈值 {CombinationWarningThreshold}，请精简维度/选项");

        return issues;
    }
}
