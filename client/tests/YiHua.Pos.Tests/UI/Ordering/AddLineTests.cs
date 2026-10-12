using Xunit;
using YiHua.Pos.UI.ViewModels.Ordering;
using static YiHua.Pos.Tests.UI.Ordering.OrderingTestFactory;

namespace YiHua.Pos.Tests.UI.Ordering;

public class AddLineTests
{
    [Fact]
    public void AddProduct_NoSpec_AddsDirectly()
    {
        var vm = CreateVm();
        var action = vm.AddProduct(Noodle());

        Assert.Equal(AddProductAction.Added, action);
        Assert.Single(vm.Lines);
        Assert.Equal("牛肉面", vm.Lines[0].ProductName);
        Assert.Equal(18m, vm.Lines[0].Amount);
    }

    [Fact]
    public void AddProduct_SameProduct_AccumulatesQty()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        vm.AddProduct(Noodle());

        Assert.Single(vm.Lines);
        Assert.Equal(2m, vm.Lines[0].Qty);
        Assert.Equal(36m, vm.Lines[0].Amount);
    }

    [Fact]
    public void AddProduct_WithSpec_Returns_NeedSpec()
    {
        var vm = CreateVm();
        var action = vm.AddProduct(KungPao());

        Assert.Equal(AddProductAction.NeedSpec, action);
        Assert.Empty(vm.Lines); // 等用户选完规格才加
    }

    [Fact]
    public void AddLine_SameSpec_Accumulates_DifferentSpec_NewLine()
    {
        var vm = CreateVm();

        vm.AddLine(new OrderLineViewModel { ProductId = 2, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, SpecText = "微辣" });
        vm.AddLine(new OrderLineViewModel { ProductId = 2, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, SpecText = "微辣" });
        Assert.Single(vm.Lines);
        Assert.Equal(2m, vm.Lines[0].Qty);

        vm.AddLine(new OrderLineViewModel { ProductId = 2, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, SpecText = "特辣" });
        Assert.Equal(2, vm.Lines.Count); // 不同规格另起一行
    }

    [Fact]
    public void AddProduct_Weighted_Returns_NeedWeight()
    {
        var vm = CreateVm();
        var action = vm.AddProduct(Seafood());

        Assert.Equal(AddProductAction.NeedWeight, action);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void AddProduct_SoldOut_Returns_SoldOut()
    {
        var vm = CreateVm();
        var action = vm.AddProduct(SoldOut());

        Assert.Equal(AddProductAction.SoldOut, action);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void DecrementLine_To_Zero_Removes_Line()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        vm.DecrementLine(line);

        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void Totals_Updated_After_Add()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());  // 18
        vm.AddProduct(Noodle());  // +18 = 36

        Assert.Equal(36m, vm.Totals.Payable);
        Assert.Equal(36m, vm.Totals.OriginalTotal);
    }
}
