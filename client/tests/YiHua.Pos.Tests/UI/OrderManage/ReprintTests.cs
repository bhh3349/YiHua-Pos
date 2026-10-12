using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;


/// <summary>重打测试：不改状态 + 记录</summary>
public class ReprintTests
{
    [Fact]
    public void Reprint_Does_Not_Change_Status_And_Records()
    {
        var order = new Order { Id = 1, OrderNo = "A01", Status = OrderStatus.Settled };
        var vm = new ReprintViewModel(order, "张三") { ReprintType = "小票" };
        ReprintRecord? record = null;
        vm.Reprinted += (s, r) => record = r;

        bool ok = vm.Confirm();

        Assert.True(ok);
        Assert.Equal(OrderStatus.Settled, order.Status); // 状态不变
        Assert.NotNull(record);
        Assert.Equal("张三", record!.OperatorName);
        Assert.Equal("小票", record.ReprintType);
    }

    [Fact]
    public void Reprint_Void_Rejected()
    {
        var order = new Order { Id = 1, OrderNo = "X", Status = OrderStatus.Void };
        var vm = new ReprintViewModel(order, "张三");

        Assert.False(vm.Confirm());
    }
}
