using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.SoldOut;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

// ViewModels.Product 命名空间已存在，用别名避免 CS0118
using Product = YiHua.Pos.Domain.Entities.Product;

public class SoldOutCalculatorTests
{
    private static List<Product> SampleProducts() => new()
    {
        new Product { Id = 1, Name = "牛肉面", CategoryId = 10, CategoryName = "热菜" },
        new Product { Id = 2, Name = "鸡汤面", CategoryId = 10, CategoryName = "热菜" },
        new Product { Id = 3, Name = "可乐", CategoryId = 20, CategoryName = "饮品" },
    };

    [Fact]
    public void CanOrder_SoldOut_Rejected()
    {
        Assert.False(SoldOutCalculator.CanOrder(true, out string? reason));
        Assert.Equal("该商品已沽清", reason);
    }

    [Fact]
    public void CanOrder_Available_Ok()
    {
        Assert.True(SoldOutCalculator.CanOrder(false, out string? reason));
        Assert.Null(reason);
    }

    [Fact]
    public void FilterByCategory_Only_That_Category()
    {
        var ids = SoldOutCalculator.FilterByCategory(SampleProducts(), 10);
        Assert.Equal(new[] { 1L, 2L }, ids);
    }

    [Fact]
    public void FilterByKeyword_Matches()
    {
        var ids = SoldOutCalculator.FilterByKeyword(SampleProducts(), "面");
        Assert.Equal(new[] { 1L, 2L }, ids);
    }

    [Fact]
    public void FilterByKeyword_No_Match_Returns_Empty_No_Error()
    {
        var ids = SoldOutCalculator.FilterByKeyword(SampleProducts(), "不存在的菜");
        Assert.Empty(ids);
    }

    [Fact]
    public void FilterByKeyword_Empty_Returns_Empty()
    {
        Assert.Empty(SoldOutCalculator.FilterByKeyword(SampleProducts(), ""));
        Assert.Empty(SoldOutCalculator.FilterByKeyword(SampleProducts(), "  "));
    }

    [Fact]
    public void ShouldRestore_Manual_Before_Reset_Not_Restored()
    {
        // 手工沽清：昨天 20:00 沽清，现在是今天 08:00（还没到 09:00）→ 不恢复
        var record = new SoldOutRecord
        {
            Source = SoldOutSource.Manual,
            SoldOutTime = new DateTime(2026, 10, 11, 20, 0, 0),
        };
        var now = new DateTime(2026, 10, 12, 8, 0, 0);
        Assert.False(SoldOutCalculator.ShouldRestore(record, now, new TimeSpan(9, 0, 0)));
    }

    [Fact]
    public void ShouldRestore_Manual_After_Reset_Restored()
    {
        // 手工沽清：昨天 20:00 沽清，现在是今天 10:00（过了 09:00）→ 恢复
        var record = new SoldOutRecord
        {
            Source = SoldOutSource.Manual,
            SoldOutTime = new DateTime(2026, 10, 11, 20, 0, 0),
        };
        var now = new DateTime(2026, 10, 12, 10, 0, 0);
        Assert.True(SoldOutCalculator.ShouldRestore(record, now, new TimeSpan(9, 0, 0)));
    }

    [Fact]
    public void ShouldRestore_Scheduled_At_Time()
    {
        var record = new SoldOutRecord
        {
            Source = SoldOutSource.Scheduled,
            SoldOutTime = new DateTime(2026, 10, 12, 11, 0, 0),
            ScheduledRestoreTime = new DateTime(2026, 10, 12, 14, 0, 0),
        };
        Assert.False(SoldOutCalculator.ShouldRestore(
            record, new DateTime(2026, 10, 12, 13, 59, 0), new TimeSpan(9, 0, 0)));
        Assert.True(SoldOutCalculator.ShouldRestore(
            record, new DateTime(2026, 10, 12, 14, 0, 0), new TimeSpan(9, 0, 0)));
    }

    [Fact]
    public void InSoldOutWindow_Lunch()
    {
        var windows = new List<SoldOutTimeWindow>
        {
            new() { Start = new TimeSpan(11, 0, 0), End = new TimeSpan(14, 0, 0), Name = "午市" },
        };
        Assert.True(SoldOutCalculator.InSoldOutWindow(new TimeSpan(12, 0, 0), windows));
        Assert.False(SoldOutCalculator.InSoldOutWindow(new TimeSpan(15, 0, 0), windows));
        Assert.False(SoldOutCalculator.InSoldOutWindow(new TimeSpan(10, 59, 0), windows));
    }

    [Fact]
    public void InSoldOutWindow_CrossMidnight()
    {
        var windows = new List<SoldOutTimeWindow>
        {
            new() { Start = new TimeSpan(22, 0, 0), End = new TimeSpan(2, 0, 0), Name = "夜宵" },
        };
        Assert.True(SoldOutCalculator.InSoldOutWindow(new TimeSpan(23, 0, 0), windows));
        Assert.True(SoldOutCalculator.InSoldOutWindow(new TimeSpan(1, 0, 0), windows));
        Assert.False(SoldOutCalculator.InSoldOutWindow(new TimeSpan(12, 0, 0), windows));
    }

    [Fact]
    public void InSoldOutWindowForCategory_Filters()
    {
        var windows = new List<SoldOutTimeWindow>
        {
            new() { Start = new TimeSpan(11, 0, 0), End = new TimeSpan(14, 0, 0),
                    CategoryIds = new List<long> { 10 } },
        };
        var now = new TimeSpan(12, 0, 0);
        Assert.True(SoldOutCalculator.InSoldOutWindowForCategory(now, 10, windows));
        Assert.False(SoldOutCalculator.InSoldOutWindowForCategory(now, 20, windows));
    }
}
