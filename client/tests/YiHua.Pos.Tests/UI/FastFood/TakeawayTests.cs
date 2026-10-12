using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.Tests.UI.FastFood;

public class TakeawayTests
{
    [Fact]
    public void Default_Is_DineIn()
    {
        var vm = new TakeawayViewModel();
        Assert.Equal(OrderType.DineIn, vm.OrderType);
        Assert.True(vm.IsDineIn);
        Assert.False(vm.NeedsTableware);
        Assert.Equal("堂食", vm.OrderTypeText);
    }

    [Fact]
    public void TakeAway_Needs_Tableware()
    {
        var vm = new TakeawayViewModel { OrderType = OrderType.TakeAway };
        Assert.True(vm.IsTakeAway);
        Assert.True(vm.NeedsTableware);
        Assert.Equal("外带", vm.OrderTypeText);
    }

    [Fact]
    public void Delivery_Needs_Tableware()
    {
        var vm = new TakeawayViewModel { OrderType = OrderType.Delivery };
        Assert.True(vm.IsDelivery);
        Assert.True(vm.NeedsTableware);
    }

    [Fact]
    public void TablewareCount_Not_Negative()
    {
        var vm = new TakeawayViewModel();
        vm.TablewareCount = 5;
        Assert.Equal(5, vm.TablewareCount);
        vm.TablewareCount = -3;
        Assert.Equal(0, vm.TablewareCount);
    }

    [Fact]
    public void Delivery_Requires_Address_And_Phone()
    {
        var vm = new DeliveryViewModel();
        Assert.False(vm.IsValid);

        vm.Address = "中山路 123 号";
        Assert.False(vm.IsValid); // 缺电话

        vm.Phone = "13800138000";
        Assert.True(vm.IsValid);
    }
}
