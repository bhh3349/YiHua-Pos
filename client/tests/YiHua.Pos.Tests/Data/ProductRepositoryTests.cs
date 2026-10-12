using Xunit;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.Tests.Data;

public class ProductRepositoryTests : TempDbFixture
{
    private readonly ProductRepository _repo;

    public ProductRepositoryTests()
    {
        _repo = new ProductRepository(Ctx.Db);
    }

    private static Product NewProduct(long id, string name, string pinyin, string code, long categoryId = 1) => new()
    {
        Id = id,
        Name = name,
        PrePinYin = pinyin,
        Code = code,
        Barcode = $"69000000000{id}",
        CategoryId = categoryId,
        Price = 10m + id,
        DataStatus = 1,
        Sort = (int)id,
    };

    [Fact]
    public async Task ReplaceAll_ReplacesExactly()
    {
        await _repo.ReplaceAllAsync(new[]
        {
            NewProduct(1, "宫保鸡丁", "GBJD", "P001"),
            NewProduct(2, "鱼香肉丝", "YXRS", "P002"),
            NewProduct(3, "米饭", "MF", "P003"),
        });
        Assert.Equal(3, (await _repo.GetAllAsync()).Count);

        // 再次替换为 2 条，旧数据必须清干净
        await _repo.ReplaceAllAsync(new[]
        {
            NewProduct(10, "可乐", "KL", "P010"),
            NewProduct(11, "雪碧", "XB", "P011"),
        });

        var all = await _repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.DoesNotContain(all, p => p.Name == "宫保鸡丁");
    }

    [Fact]
    public async Task Search_FindsByNamePinyinAndCode()
    {
        await _repo.ReplaceAllAsync(new[]
        {
            NewProduct(1, "宫保鸡丁", "GBJD", "P001"),
            NewProduct(2, "鱼香肉丝", "YXRS", "P002"),
        });

        var byName = await _repo.SearchAsync("宫保");
        Assert.Single(byName);

        var byPinyin = await _repo.SearchAsync("YXRS");
        Assert.Single(byPinyin);
        Assert.Equal("鱼香肉丝", byPinyin[0].Name);

        var byCode = await _repo.SearchAsync("P001");
        Assert.Single(byCode);

        Assert.Empty(await _repo.SearchAsync("   ")); // 空关键字返回空
        Assert.Empty(await _repo.SearchAsync("不存在的菜"));
    }

    [Fact]
    public async Task GetByBarcode_Works()
    {
        await _repo.ReplaceAllAsync(new[] { NewProduct(7, "测试菜", "CSC", "P007") });

        var found = await _repo.GetByBarcodeAsync("690000000007");
        Assert.NotNull(found);
        Assert.Equal("测试菜", found!.Name);

        Assert.Null(await _repo.GetByBarcodeAsync("000000"));
    }

    [Fact]
    public async Task GetByCategory_FiltersCorrectly()
    {
        await _repo.ReplaceAllAsync(new[]
        {
            NewProduct(1, "菜A", "CA", "P001", categoryId: 1),
            NewProduct(2, "菜B", "CB", "P002", categoryId: 2),
        });

        var cat1 = await _repo.GetByCategoryAsync(1);
        Assert.Single(cat1);
        Assert.Equal("菜A", cat1[0].Name);
    }
}
