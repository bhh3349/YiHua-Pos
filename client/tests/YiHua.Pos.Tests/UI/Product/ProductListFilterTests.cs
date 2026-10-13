using YiHua.Pos.UI.ViewModels.Product;
using Xunit;

namespace YiHua.Pos.Tests.UI.Product;

/// <summary>
/// 商品列表筛选测试：分类 + 搜索（含别名）+ 状态。
/// </summary>
public class ProductListFilterTests
{
    private class FakeDataSource : IProductDataSource
    {
        private readonly List<ProductDto> _products;
        public FakeDataSource(List<ProductDto> products) => _products = products;
        public Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_products);
        public Task<List<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
            => Task.FromResult(_products.Where(p => p.CategoryId == categoryId).ToList());
        public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
            => Task.FromResult(new List<CategoryDto>());
        public Task<List<UnitDto>> GetUnitsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<UnitDto>());

        public Task<List<ProductDto>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(_products.Where(p => p.Name.Contains(keyword)).Take(limit).ToList());

        public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
        {
            var p = _products.FirstOrDefault(x => x.Id == productId);
            if (p != null) p.IsSoldOut = soldOut;
            return Task.CompletedTask;
        }

        public Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
            => Task.FromResult(ids.Count());

        public Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default)
            => Task.FromResult(ids.Count());
    }

    private static ProductListViewModel CreateVm()
    {
        var products = new List<ProductDto>
        {
            new() { Id = 1, Name = "兰州牛肉面(小)", CategoryId = 1, PrePinYin = "lz", Sort = 1 },
            new() { Id = 2, Name = "宫保鸡丁", CategoryId = 1, PrePinYin = "gb", Sort = 2 },
            new() { Id = 3, Name = "珍珠奶茶", CategoryId = 2, PrePinYin = "zz", Sort = 3 },
        };
        var vm = new ProductListViewModel(new FakeDataSource(products));
        vm.LoadAsync().Wait();
        // 注册别名：客人说"牛肉面"→"兰州牛肉面(小)"
        vm.RegisterAliases(1, new[] { "牛肉面", "牛肉拉面" });
        return vm;
    }

    [Fact]
    public void Filter_ByCategory()
    {
        var vm = CreateVm();
        vm.SelectedCategoryId = 1;
        var result = vm.GetFiltered();
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(1, p.CategoryId));
    }

    [Fact]
    public void Search_ByName()
    {
        var vm = CreateVm();
        vm.SearchKeyword = "宫保";
        var result = vm.GetFiltered();
        Assert.Single(result);
        Assert.Equal("宫保鸡丁", result[0].Name);
    }

    [Fact]
    public void Search_ByAlias_FindsProduct()
    {
        var vm = CreateVm();
        vm.SearchKeyword = "牛肉面"; // 别名
        var result = vm.GetFiltered();
        Assert.Single(result);
        Assert.Equal("兰州牛肉面(小)", result[0].Name);
    }

    [Fact]
    public void Search_ByPinyin()
    {
        var vm = CreateVm();
        vm.SearchKeyword = "zz";
        var result = vm.GetFiltered();
        Assert.Single(result);
        Assert.Equal("珍珠奶茶", result[0].Name);
    }

    [Fact]
    public void Filter_BySoldOutStatus()
    {
        var vm = CreateVm();
        vm.SoldOutIds.Add(1); // 牛肉面沽清
        vm.StatusFilter = ProductStatusFilter.SoldOut;
        var result = vm.GetFiltered();
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);

        vm.StatusFilter = ProductStatusFilter.OnSale;
        result = vm.GetFiltered();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Id == 1);
    }

    [Fact]
    public void Filter_All_ReturnsEverything()
    {
        var vm = CreateVm();
        vm.StatusFilter = ProductStatusFilter.All;
        Assert.Equal(3, vm.GetFiltered().Count);
    }
}
