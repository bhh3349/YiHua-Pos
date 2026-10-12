using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.SoldOut;
using YiHua.Pos.UI.ViewModels.SoldOut;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

// ViewModels.Product 命名空间已存在，用别名避免 CS0118
using Product = YiHua.Pos.Domain.Entities.Product;

public class SoldOutBatchTests
{
    private static List<Product> SampleProducts() => new()
    {
        new Product { Id = 1, Name = "牛肉面", CategoryId = 10, CategoryName = "热菜" },
        new Product { Id = 2, Name = "鸡汤面", CategoryId = 10, CategoryName = "热菜" },
        new Product { Id = 3, Name = "可乐", CategoryId = 20, CategoryName = "饮品" },
    };

    private static SoldOutBatchViewModel CreateVm(out FakeSoldOutRepository repo)
    {
        repo = new FakeSoldOutRepository();
        var service = new SoldOutService(repo, new SystemClock(),
            new[] { new SelfPlatformSoldOutSync() });
        var vm = new SoldOutBatchViewModel(service)
        {
            AllProducts = SampleProducts(),
            OperatorName = "张三",
        };
        return vm;
    }

    [Fact]
    public void SelectByCategory_Sets_ConfirmText()
    {
        var vm = CreateVm(out _);
        vm.SelectByCategory(10, "热菜档");

        Assert.Equal(2, vm.SelectedIds.Count);
        Assert.Contains("热菜档", vm.ConfirmText);
        Assert.Contains("2", vm.ConfirmText);
    }

    [Fact]
    public void SelectByKeyword_Matches()
    {
        var vm = CreateVm(out _);
        vm.Keyword = "面";
        vm.SelectByKeyword();

        Assert.Equal(2, vm.SelectedIds.Count);
        Assert.Contains("面", vm.ConfirmText!);
    }

    [Fact]
    public void SelectByKeyword_No_Match_Shows_Hint()
    {
        var vm = CreateVm(out _);
        vm.Keyword = "不存在";
        vm.SelectByKeyword();

        Assert.Empty(vm.SelectedIds);
        Assert.Contains("未找到", vm.ConfirmText!);
    }

    [Fact]
    public async Task ConfirmBatch_Sells_Out_Selected()
    {
        var vm = CreateVm(out var repo);
        vm.SelectByCategory(10, "热菜档");

        int count = await vm.ConfirmBatchAsync();

        Assert.Equal(2, count);
        Assert.Equal(2, await repo.CountAsync());
        Assert.True(await repo.GetByProductIdAsync(1) != null);
    }

    [Fact]
    public async Task ConfirmBatch_Empty_Rejected()
    {
        var vm = CreateVm(out _);
        int count = await vm.ConfirmBatchAsync();

        Assert.Equal(0, count);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task BatchSellOut_Is_Manual_Source()
    {
        var vm = CreateVm(out var repo);
        vm.SelectByCategory(10, "热菜档");
        await vm.ConfirmBatchAsync();

        var record = await repo.GetByProductIdAsync(1);
        Assert.NotNull(record);
        Assert.Equal(SoldOutSource.Manual, record.Source);
        Assert.Equal("张三", record.OperatorName);
    }
}
