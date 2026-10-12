using Xunit;
using YiHua.Pos.UI.ViewModels.Ordering;
using static YiHua.Pos.Tests.UI.Ordering.OrderingTestFactory;

namespace YiHua.Pos.Tests.UI.Ordering;

public class PriceEditPermissionTests
{
    [Fact]
    public void Cashier_Cannot_Edit_Price()
    {
        var vm = CreateVm(role: OperatorRole.Cashier);
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        bool ok = vm.TryEditPrice(line, 20m, out string? reason);

        Assert.False(ok);
        Assert.NotNull(reason);
        Assert.Equal(18m, line.Price); // 价格未变
    }

    [Fact]
    public void Manager_Can_Edit_Price()
    {
        var vm = CreateVm(role: OperatorRole.Manager);
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        bool ok = vm.TryEditPrice(line, 20m, out string? reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.Equal(20m, line.Price);
        Assert.Equal(20m, vm.Totals.Payable);
    }

    [Fact]
    public void Cashier_Cannot_Toggle_Gift()
    {
        var vm = CreateVm(role: OperatorRole.Cashier);
        vm.AddProduct(Noodle());

        bool ok = vm.TryToggleGift(vm.Lines[0], out _);

        Assert.False(ok);
        Assert.False(vm.Lines[0].IsGift);
    }

    [Fact]
    public void Manager_Can_Toggle_Gift()
    {
        var vm = CreateVm(role: OperatorRole.Manager);
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        Assert.True(vm.TryToggleGift(line, out _));
        Assert.True(line.IsGift);
        Assert.Equal(0m, line.Amount);
        Assert.Equal(0m, vm.Totals.Payable);      // 应付 0
        Assert.Equal(18m, vm.Totals.OriginalTotal); // 原价保留
    }

    [Fact]
    public void Cashier_Cannot_Set_Order_Discount()
    {
        var vm = CreateVm(role: OperatorRole.Cashier);
        bool ok = vm.TrySetOrderDiscountRate(0.9m, out _);

        Assert.False(ok);
        Assert.Equal(1m, vm.OrderDiscountRate);
    }

    [Fact]
    public void Manager_Can_Set_Order_Discount()
    {
        var vm = CreateVm(role: OperatorRole.Manager);
        vm.AddProduct(Noodle()); // 18

        Assert.True(vm.TrySetOrderDiscountRate(0.9m, out _));
        Assert.Equal(16.20m, vm.Totals.Payable); // 18 × 0.9
    }

    [Fact]
    public void Manager_Rejects_Invalid_Discount_Rate()
    {
        var vm = CreateVm(role: OperatorRole.Manager);
        Assert.False(vm.TrySetOrderDiscountRate(0m, out _));
        Assert.False(vm.TrySetOrderDiscountRate(1.5m, out _));
        Assert.Equal(1m, vm.OrderDiscountRate);
    }
}
