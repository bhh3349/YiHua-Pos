using YiHua.Pos.UI.ViewModels.Product;
using Xunit;

namespace YiHua.Pos.Tests.UI.Product;

/// <summary>
/// 商品编辑校验测试。
/// </summary>
public class ProductEditValidationTests
{
    private class FakeEditService : IProductEditService
    {
        public Task SaveAsync(ProductEditModel model, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long productId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> SaveCategoryAsync(long id, string name, int sort, CancellationToken ct = default) => Task.FromResult(id);
        public Task DeleteCategoryAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ProductEditViewModel CreateVm()
    {
        var vm = new ProductEditViewModel(new FakeEditService());
        vm.LoadForEdit(null);
        return vm;
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        var vm = CreateVm();
        vm.Model.Name = "";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 1;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("名称"));
    }

    [Fact]
    public void Validate_NoCategory_Fails()
    {
        var vm = CreateVm();
        vm.Model.Name = "牛肉面";
        vm.Model.CategoryId = 0;
        vm.Model.UnitId = 1;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("分类"));
    }

    [Fact]
    public void Validate_NoUnit_Fails()
    {
        var vm = CreateVm();
        vm.Model.Name = "牛肉面";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 0;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("单位"));
    }

    [Fact]
    public void Validate_NegativePrice_Fails()
    {
        var vm = CreateVm();
        vm.Model.Name = "牛肉面";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 1;
        vm.Model.Price = -5m;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("负数"));
    }

    [Fact]
    public void Validate_ValidProduct_Passes()
    {
        var vm = CreateVm();
        vm.Model.Name = "牛肉面";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 1;
        vm.Model.Price = 18m;
        Assert.Empty(vm.Validate());
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void Validate_Bundle_RequiresItems()
    {
        var vm = CreateVm();
        vm.Model.Name = "双人套餐A";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 1;
        vm.Model.IsBundle = true;
        vm.Model.BundlePrice = 88m;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("至少"));
    }

    [Fact]
    public void Validate_Bundle_RequiresPrice()
    {
        var vm = CreateVm();
        vm.Model.Name = "双人套餐A";
        vm.Model.CategoryId = 1;
        vm.Model.UnitId = 1;
        vm.Model.IsBundle = true;
        vm.Model.BundleItems.Add(new BundleItem { ProductId = 1, ProductName = "牛肉面" });
        vm.Model.BundlePrice = 0;
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("售价"));
    }

    [Fact]
    public void GetDeleteWarning_WithHistory_SuggestsDeactivate()
    {
        var warning = ProductEditViewModel.GetDeleteWarning(hasOrderHistory: true);
        Assert.Contains("停用", warning);
        Assert.Contains("历史订单", warning);
    }

    [Fact]
    public void GetDeleteWarning_WithoutHistory_SimpleConfirm()
    {
        var warning = ProductEditViewModel.GetDeleteWarning(hasOrderHistory: false);
        Assert.Contains("确定删除", warning);
    }
}
