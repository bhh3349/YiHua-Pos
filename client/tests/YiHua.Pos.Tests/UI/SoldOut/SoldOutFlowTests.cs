using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.SoldOut;
using YiHua.Pos.UI.ViewModels.SoldOut;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

// ViewModels.Product 命名空间已存在，用别名避免 CS0118
using Product = YiHua.Pos.Domain.Entities.Product;

/// <summary>完整流程：沽清 → 点单拒绝 → 恢复 → 可点单 → 定时还原</summary>
public class SoldOutFlowTests
{
    private static Product Noodle() => new()
    {
        Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
        CategoryId = 10, CategoryName = "热菜", ProductType = 1, DataStatus = 1,
    };

    [Fact]
    public async Task Full_Flow_SellOut_Reject_Restore_Accept()
    {
        var repo = new FakeSoldOutRepository();
        var service = new SoldOutService(repo, new SystemClock(),
            new[] { new SelfPlatformSoldOutSync() });
        var product = Noodle();

        // 1. 沽清
        await service.SellOutAsync(product, "张三");
        Assert.True(await service.IsSoldOutAsync(1));

        // 2. 点单被拒绝
        var (canOrder, reason) = await service.CanOrderAsync(1);
        Assert.False(canOrder);
        Assert.Equal("该商品已沽清", reason);

        // 3. 恢复供应
        await service.RestoreAsync(1);
        Assert.False(await service.IsSoldOutAsync(1));

        // 4. 可点单
        var (canOrder2, _) = await service.CanOrderAsync(1);
        Assert.True(canOrder2);
    }

    [Fact]
    public async Task Manual_And_Scheduled_Are_Separate_Records()
    {
        var repo = new FakeSoldOutRepository();

        // 手工沽清
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 1, ProductName = "牛肉面",
            Source = SoldOutSource.Manual,
            SoldOutTime = DateTime.Now,
        });

        // 定时沽清（不同商品）
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 2, ProductName = "午市套餐",
            Source = SoldOutSource.Scheduled,
            SoldOutTime = DateTime.Now,
            ScheduledRestoreTime = DateTime.Now.AddHours(3),
        });

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(SoldOutSource.Manual, all.First(r => r.ProductId == 1).Source);
        Assert.Equal(SoldOutSource.Scheduled, all.First(r => r.ProductId == 2).Source);
    }

    [Fact]
    public async Task ListViewModel_Groups_By_Category()
    {
        var repo = new FakeSoldOutRepository();
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 1, ProductName = "牛肉面",
            CategoryId = 10, CategoryName = "热菜",
            Source = SoldOutSource.Manual, SoldOutTime = DateTime.Now,
        });
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 2, ProductName = "鸡汤面",
            CategoryId = 10, CategoryName = "热菜",
            Source = SoldOutSource.Manual, SoldOutTime = DateTime.Now,
        });
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 3, ProductName = "可乐",
            CategoryId = 20, CategoryName = "饮品",
            Source = SoldOutSource.Manual, SoldOutTime = DateTime.Now,
        });

        var service = new SoldOutService(repo, new SystemClock(),
            new[] { new SelfPlatformSoldOutSync() });
        var vm = new SoldOutListViewModel(service, repo);
        await vm.LoadAsync();

        Assert.Equal(2, vm.Groups.Count); // 热菜、饮品
        var hot = vm.Groups.First(g => g.CategoryName == "热菜");
        Assert.Equal(2, hot.Count);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task RestoreAll_Clears_Everything()
    {
        var repo = new FakeSoldOutRepository();
        var service = new SoldOutService(repo, new SystemClock(),
            new[] { new SelfPlatformSoldOutSync() });

        await service.SellOutAsync(Noodle(), "张三");
        await service.SellOutAsync(new Product
        {
            Id = 2, Name = "可乐", CategoryId = 20, CategoryName = "饮品",
        }, "张三");

        int count = await service.RestoreAllAsync();
        Assert.Equal(2, count);
        Assert.Equal(0, await repo.CountAsync());
    }
}
