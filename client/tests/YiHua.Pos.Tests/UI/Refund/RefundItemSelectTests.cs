using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>退菜多选逻辑</summary>
public class RefundItemSelectTests
{
    private static Order MakeOrder() => new()
    {
        Id = 1,
        OrderNo = "A08",
        Status = OrderStatus.Settled,
        AmountReceived = 128m,
        Items = new List<OrderItem>
        {
            new() { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 2 },
            new() { Id = 2, ProductName = "饮料", Price = 8m, Qty = 1, IsRefunded = true },
            new() { Id = 3, ProductName = "凉菜", Price = 12m, Qty = 1 },
        },
    };

    [Fact]
    public void Select_Multiple_Total_Correct()
    {
        var vm = new RefundItemSelectViewModel(MakeOrder());
        vm.Items[0].Selected = true; // 36
        vm.Items[2].Selected = true; // 12
        Assert.Equal(48m, vm.TotalSelected);
    }

    [Fact]
    public void Select_None_Invalid()
    {
        var vm = new RefundItemSelectViewModel(MakeOrder());
        Assert.False(vm.ValidateAll());
        Assert.Contains("至少选择一行", vm.Error);
    }

    [Fact]
    public void AlreadyRefunded_Row_Cannot_Select_Amount()
    {
        var vm = new RefundItemSelectViewModel(MakeOrder());
        // 已退的行（饮料）如果被选中，校验应拒绝
        vm.Items[1].Selected = true;
        Assert.False(vm.ValidateAll());
        Assert.Contains("已退过", vm.Error);
    }

    [Fact]
    public void Deselect_Updates_Total()
    {
        var vm = new RefundItemSelectViewModel(MakeOrder());
        vm.Items[0].Selected = true;
        Assert.Equal(36m, vm.TotalSelected);
        vm.Items[0].Selected = false;
        Assert.Equal(0m, vm.TotalSelected);
    }
}
