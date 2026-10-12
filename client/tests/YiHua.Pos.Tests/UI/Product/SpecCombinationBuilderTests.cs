using YiHua.Pos.UI.ViewModels.Product;
using Xunit;

namespace YiHua.Pos.Tests.UI.Product;

/// <summary>
/// 规格组合生成器测试（核心逻辑）。
/// </summary>
public class SpecCombinationBuilderTests
{
    private static List<SpecDimension> TeaDimensions() => new()
    {
        new SpecDimension
        {
            Name = "温度",
            Options = new() { new() { Name = "热" }, new() { Name = "去冰" }, new() { Name = "少冰" } },
        },
        new SpecDimension
        {
            Name = "糖度",
            Options = new() { new() { Name = "全糖" }, new() { Name = "七分" }, new() { Name = "五分" } },
        },
        new SpecDimension
        {
            Name = "加料",
            AllowMultiSelect = true,
            Options = new()
            {
                new() { Name = "珍珠", PriceAdjustment = 2m },
                new() { Name = "椰果", PriceAdjustment = 2m },
            },
        },
    };

    [Fact]
    public void Build_TeaDimensions_CartesianProduct()
    {
        // 温度3 × 糖度3 × 加料幂集4 = 36
        var combos = SpecCombinationBuilder.Build(TeaDimensions());
        Assert.Equal(36, combos.Count);
    }

    [Fact]
    public void Build_PriceAdjustment_SumsCorrectly()
    {
        var combos = SpecCombinationBuilder.Build(TeaDimensions(), basePrice: 12m);
        // 找一个加珍珠+椰果的组合
        var combo = combos.First(c =>
            c.MultiSelections.TryGetValue("加料", out var adds) && adds.Count == 2);
        Assert.Equal(4m, combo.PriceAdjustment);
        Assert.Equal(16m, combo.GetPrice(12m));
    }

    [Fact]
    public void Build_NonCombinableDimension_Standalone()
    {
        // 火锅锅底：不可组合，每个选项独立
        var dims = new List<SpecDimension>
        {
            new() { Name = "锅底", IsCombinable = false, Options = new()
                { new() { Name = "麻辣" }, new() { Name = "番茄" }, new() { Name = "菌汤" } } },
        };
        var combos = SpecCombinationBuilder.Build(dims);
        Assert.Equal(3, combos.Count);
        Assert.All(combos, c => Assert.Single(c.SingleSelections));
    }

    [Fact]
    public void Build_Mixed_CombinableAndStandalone()
    {
        var dims = new List<SpecDimension>
        {
            new() { Name = "大小", Options = new() { new() { Name = "大" }, new() { Name = "小" } } },
            new() { Name = "锅底", IsCombinable = false, Options = new()
                { new() { Name = "麻辣" }, new() { Name = "番茄" } } },
        };
        var combos = SpecCombinationBuilder.Build(dims);
        // 组合维度 2 + 独立维度 2 = 4
        Assert.Equal(4, combos.Count);
    }

    [Fact]
    public void Build_EmptyDimensions_ReturnsEmpty()
    {
        Assert.Empty(SpecCombinationBuilder.Build(new List<SpecDimension>()));
        Assert.Empty(SpecCombinationBuilder.Build(null!));
    }

    [Fact]
    public void Build_DimensionWithoutOptions_Skipped()
    {
        var dims = new List<SpecDimension>
        {
            new() { Name = "空维度", Options = new() },
            new() { Name = "大小", Options = new() { new() { Name = "大" } } },
        };
        var combos = SpecCombinationBuilder.Build(dims);
        Assert.Single(combos);
    }

    [Fact]
    public void EstimateCount_MatchesActual()
    {
        var dims = TeaDimensions();
        Assert.Equal(36, SpecCombinationBuilder.EstimateCount(dims));
    }

    [Fact]
    public void ExceedsThreshold_WarnsOnExplosion()
    {
        // 4维 × 5选项 = 625，超过阈值 100
        var dims = new List<SpecDimension>();
        for (int d = 0; d < 4; d++)
        {
            var dim = new SpecDimension { Name = $"维度{d}" };
            for (int o = 0; o < 5; o++)
                dim.Options.Add(new SpecOption { Name = $"选项{o}" });
            dims.Add(dim);
        }
        Assert.True(SpecCombinationBuilder.ExceedsThreshold(dims));
        Assert.Equal(625, SpecCombinationBuilder.EstimateCount(dims));
    }

    [Fact]
    public void ExceedsThreshold_NormalCase_NoWarning()
    {
        Assert.False(SpecCombinationBuilder.ExceedsThreshold(TeaDimensions()));
    }

    [Fact]
    public void Validate_DuplicateDimensionName_ReportsIssue()
    {
        var dims = new List<SpecDimension>
        {
            new() { Name = "温度", Options = new() { new() { Name = "热" } } },
            new() { Name = "温度", Options = new() { new() { Name = "冰" } } },
        };
        var issues = SpecCombinationBuilder.Validate(dims);
        Assert.Contains(issues, i => i.Contains("重复"));
    }

    [Fact]
    public void Validate_DuplicateOptionName_ReportsIssue()
    {
        var dims = new List<SpecDimension>
        {
            new() { Name = "温度", Options = new() { new() { Name = "热" }, new() { Name = "热" } } },
        };
        var issues = SpecCombinationBuilder.Validate(dims);
        Assert.Contains(issues, i => i.Contains("重复"));
    }

    [Fact]
    public void Validate_EmptyDimensionName_ReportsIssue()
    {
        var dims = new List<SpecDimension> { new() { Name = "", Options = new() { new() { Name = "热" } } } };
        var issues = SpecCombinationBuilder.Validate(dims);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void Validate_ValidConfig_NoIssues()
    {
        var issues = SpecCombinationBuilder.Validate(TeaDimensions());
        Assert.Empty(issues);
    }

    [Fact]
    public void DisplayText_FormatsCorrectly()
    {
        var combos = SpecCombinationBuilder.Build(TeaDimensions());
        var combo = combos.First(c =>
            c.SingleSelections.GetValueOrDefault("温度") == "少冰" &&
            c.SingleSelections.GetValueOrDefault("糖度") == "七分" &&
            c.MultiSelections.TryGetValue("加料", out var adds) && adds.Contains("珍珠"));
        Assert.Contains("少冰", combo.DisplayText);
        Assert.Contains("七分", combo.DisplayText);
        Assert.Contains("珍珠", combo.DisplayText);
    }
}
