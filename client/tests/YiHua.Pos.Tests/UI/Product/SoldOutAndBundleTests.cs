using YiHua.Pos.UI.ViewModels.Product;
using Xunit;

namespace YiHua.Pos.Tests.UI.Product;

/// <summary>
/// 沽清切换测试 + 套餐定价测试。
/// </summary>
public class SoldOutToggleTests
{
    private class FakeEditService : IProductEditService
    {
        public Task SaveAsync(ProductEditModel model, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long productId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void ToggleSoldOut_TogglesState()
    {
        var vm = new SoldOutViewModel(new FakeEditService());
        Assert.True(vm.ToggleSoldOut(1));  // → 沽清
        Assert.True(vm.SoldOutIds.Contains(1));
        Assert.False(vm.ToggleSoldOut(1)); // → 恢复
        Assert.False(vm.SoldOutIds.Contains(1));
    }

    [Fact]
    public void ToggleSoldOut_RecordsTime()
    {
        var vm = new SoldOutViewModel(new FakeEditService());
        vm.ToggleSoldOut(5);
        Assert.True(vm.SoldOutTimes.ContainsKey(5));
        vm.ToggleSoldOut(5); // 恢复
        Assert.False(vm.SoldOutTimes.ContainsKey(5));
    }

    [Fact]
    public void BatchSoldOut_AddsMultiple()
    {
        var vm = new SoldOutViewModel(new FakeEditService());
        vm.BatchSoldOut(new[] { 1L, 2L, 3L });
        Assert.Equal(3, vm.SoldOutIds.Count);
    }

    [Fact]
    public void BatchRestore_RemovesMultiple()
    {
        var vm = new SoldOutViewModel(new FakeEditService());
        vm.BatchSoldOut(new[] { 1L, 2L, 3L });
        vm.BatchRestore(new[] { 1L, 2L });
        Assert.Single(vm.SoldOutIds);
        Assert.True(vm.SoldOutIds.Contains(3));
    }

    [Fact]
    public void RestoreAll_ClearsEverything()
    {
        var vm = new SoldOutViewModel(new FakeEditService());
        vm.BatchSoldOut(new[] { 1L, 2L });
        vm.RestoreAll();
        Assert.Empty(vm.SoldOutIds);
        Assert.Empty(vm.SoldOutTimes);
    }
}

/// <summary>
/// 套餐定价测试：售价≠单品相加，沽清联动。
/// </summary>
public class BundlePricingTests
{
    private static ProductDto MakeProduct(long id, string name, decimal price)
        => new() { Id = id, Name = name, Price = price, UnitName = "份" };

    [Fact]
    public void BundlePrice_IndependentOfItemsTotal()
    {
        var vm = new ProductBundleEditorViewModel();
        vm.AddItem(MakeProduct(1, "牛肉面", 18m));
        vm.AddItem(MakeProduct(2, "小菜", 8m));
        vm.AddItem(MakeProduct(3, "饮料", 6m));
        // 单品总价 32，套餐价 28（优惠）
        vm.BundlePrice = 28m;
        Assert.Equal(32m, vm.ItemsTotal);
        Assert.Equal(28m, vm.BundlePrice);
        Assert.Equal(4m, vm.DiscountAmount); // 优惠 4 元
    }

    [Fact]
    public void AddItem_Duplicate_NotAddedTwice()
    {
        var vm = new ProductBundleEditorViewModel();
        var p = MakeProduct(1, "牛肉面", 18m);
        vm.AddItem(p);
        vm.AddItem(p);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void AddItem_Quantity_FixedAtOne()
    {
        var vm = new ProductBundleEditorViewModel();
        vm.AddItem(MakeProduct(1, "牛肉面", 18m));
        Assert.Equal(1m, vm.Items[0].Quantity); // 数量固定，不可改
    }

    [Fact]
    public void IsAvailable_SoldOutItem_MakesBundleUnavailable()
    {
        var vm = new ProductBundleEditorViewModel();
        vm.AddItem(MakeProduct(1, "牛肉面", 18m));
        vm.AddItem(MakeProduct(2, "小菜", 8m));
        // 牛肉面沽清 → 套餐不可售
        Assert.False(vm.IsAvailable(new[] { 1L }));
        Assert.True(vm.IsAvailable(new[] { 99L })); // 其他商品沽清不影响
    }

    [Fact]
    public void UpdateSoldOutStatus_MarksItems()
    {
        var vm = new ProductBundleEditorViewModel();
        vm.AddItem(MakeProduct(1, "牛肉面", 18m));
        vm.UpdateSoldOutStatus(new[] { 1L });
        Assert.True(vm.Items[0].IsSoldOut);
    }

    [Fact]
    public void Validate_EmptyBundle_Fails()
    {
        var vm = new ProductBundleEditorViewModel { BundlePrice = 28m };
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("至少"));
    }

    [Fact]
    public void RemoveItem_RemovesFromBundle()
    {
        var vm = new ProductBundleEditorViewModel();
        vm.AddItem(MakeProduct(1, "牛肉面", 18m));
        vm.RemoveItem(1);
        Assert.Empty(vm.Items);
    }
}
