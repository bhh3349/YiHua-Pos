using YiHua.Pos.UI.ViewModels.Product;
using Xunit;

namespace YiHua.Pos.Tests.UI.Product;

/// <summary>
/// 沽清持久化测试。
///
/// 这组守的是一条真实事故：之前沽清状态只存在内存 HashSet 里，
/// 老板早上沽清三道菜、晚上重启软件全丢，客人点了沽清的菜照样能下单。
/// 现在沽清必须落到商品表（IsSoldOut + SoldOutAt）。
/// </summary>
public class SoldOutPersistenceTests
{
    private sealed class FakeDataSource : IProductDataSource
    {
        public List<ProductDto> Products { get; } = new();
        public List<(long Id, bool SoldOut)> Calls { get; } = new();

        public Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Products.ToList());
        public Task<List<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
            => Task.FromResult(Products.Where(p => p.CategoryId == categoryId).ToList());
        public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) => Task.FromResult(new List<CategoryDto>());
        public Task<List<UnitDto>> GetUnitsAsync(CancellationToken ct = default) => Task.FromResult(new List<UnitDto>());
        public Task<List<ProductDto>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(Products.Where(p => p.Name.Contains(keyword)).Take(limit).ToList());

        public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
        {
            Calls.Add((productId, soldOut));
            var p = Products.FirstOrDefault(x => x.Id == productId);
            if (p != null) { p.IsSoldOut = soldOut; p.SoldOutAt = soldOut ? DateTime.Now : null; }
            return Task.CompletedTask;
        }

        public Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
            => Task.FromResult(ids.Count());
        public Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default)
            => Task.FromResult(ids.Count());
    }

    private sealed class FakeEditService : IProductEditService
    {
        public List<(long Id, bool SoldOut)> Calls { get; } = new();
        public Task SaveAsync(ProductEditModel model, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long productId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
        {
            Calls.Add((productId, soldOut));
            return Task.CompletedTask;
        }
        public Task<long> SaveCategoryAsync(long id, string name, int sort, CancellationToken ct = default) => Task.FromResult(id);
        public Task DeleteCategoryAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static FakeDataSource SourceWith(params (long Id, string Name, bool SoldOut)[] items)
    {
        var src = new FakeDataSource();
        foreach (var (id, name, soldOut) in items)
            src.Products.Add(new ProductDto { Id = id, Name = name, CategoryId = 1, IsSoldOut = soldOut, DataStatus = 1 });
        return src;
    }

    [Fact]
    public async Task 设置沽清_会写库_不只是改内存()
    {
        var src = SourceWith((1, "牛肉面", false));
        var vm = new ProductListViewModel(src);
        await vm.LoadAsync();

        await vm.SetSoldOutAsync(1, true);

        Assert.Contains((1L, true), src.Calls);
        Assert.True(src.Products[0].IsSoldOut);
        Assert.NotNull(src.Products[0].SoldOutAt);
    }

    [Fact]
    public async Task 重新加载后_沽清状态仍在()
    {
        var src = SourceWith((1, "牛肉面", false), (2, "宫保鸡丁", false));
        var vm = new ProductListViewModel(src);
        await vm.LoadAsync();
        await vm.SetSoldOutAsync(1, true);

        // 模拟重启：新建一个 VM 从同一个数据源载入
        var reopened = new ProductListViewModel(src);
        await reopened.LoadAsync();

        Assert.True(reopened.IsSoldOut(1));
        Assert.False(reopened.IsSoldOut(2));
    }

    [Fact]
    public async Task 恢复沽清_清掉沽清时间()
    {
        var src = SourceWith((1, "牛肉面", true));
        var vm = new ProductListViewModel(src);
        await vm.LoadAsync();

        await vm.SetSoldOutAsync(1, false);

        Assert.Contains((1L, false), src.Calls);
        Assert.False(src.Products[0].IsSoldOut);
        Assert.Null(src.Products[0].SoldOutAt);
    }

    [Fact]
    public async Task 沽清的商品_不出现在在售筛选里()
    {
        var src = SourceWith((1, "牛肉面", false), (2, "宫保鸡丁", false));
        var vm = new ProductListViewModel(src);
        await vm.LoadAsync();
        await vm.SetSoldOutAsync(1, true);

        vm.StatusFilter = ProductStatusFilter.OnSale;
        var onSale = vm.GetFiltered();
        Assert.Single(onSale);
        Assert.Equal(2, onSale[0].Id);

        vm.StatusFilter = ProductStatusFilter.SoldOut;
        var soldOut = vm.GetFiltered();
        Assert.Single(soldOut);
        Assert.Equal(1, soldOut[0].Id);
    }

    [Fact]
    public async Task 下架与沽清是两回事()
    {
        var src = SourceWith((1, "沽清菜", false), (2, "下架菜", false));
        src.Products[1].DataStatus = 99; // 下架

        var vm = new ProductListViewModel(src);
        await vm.LoadAsync();
        await vm.SetSoldOutAsync(1, true); // 沽清

        // 下架筛选项里只有真正下架的那个，不含沽清的
        vm.StatusFilter = ProductStatusFilter.OffShelf;
        var offShelf = vm.GetFiltered();
        Assert.Single(offShelf);
        Assert.Equal(2, offShelf[0].Id);

        // 沽清筛选项里只有沽清的那个，不含下架的
        vm.StatusFilter = ProductStatusFilter.SoldOut;
        var soldOut = vm.GetFiltered();
        Assert.Single(soldOut);
        Assert.Equal(1, soldOut[0].Id);
    }

    [Fact]
    public async Task 沽清管理页_从商品表载入()
    {
        var src = SourceWith((1, "牛肉面", true), (2, "宫保鸡丁", false));
        var edit = new FakeEditService();
        var vm = new SoldOutViewModel(edit, src);

        await vm.LoadAsync();

        Assert.Contains(1L, vm.SoldOutIds);
        Assert.DoesNotContain(2L, vm.SoldOutIds);
    }

    [Fact]
    public void 切换沽清_会落库()
    {
        var edit = new FakeEditService();
        var vm = new SoldOutViewModel(edit);

        vm.ToggleSoldOut(7);
        Assert.Contains((7L, true), edit.Calls);

        vm.ToggleSoldOut(7);
        Assert.Contains((7L, false), edit.Calls);
    }

    [Fact]
    public void 全部恢复_每个都落库()
    {
        var edit = new FakeEditService();
        var vm = new SoldOutViewModel(edit);
        vm.BatchSoldOut(new[] { 1L, 2L, 3L });

        vm.RestoreAll();

        foreach (var id in new[] { 1L, 2L, 3L })
            Assert.Contains((id, false), edit.Calls);
    }
}