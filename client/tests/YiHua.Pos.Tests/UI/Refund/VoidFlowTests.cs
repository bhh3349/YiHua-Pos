using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>冲正流程（含桌台释放）</summary>
public class VoidFlowTests
{
    [Fact]
    public void Void_Pending_Releases_Desk()
    {
        var order = new Order { Id = 1, OrderNo = "A08", Status = OrderStatus.Pending, DeskId = 8 };
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L);
        var (ok, error, deskId) = vm.VoidOrder("录错单");
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(8, deskId); // 返回桌台 ID，调用方释放桌台
        Assert.Equal(OrderStatus.Void, order.Status);
        Assert.Contains("[冲正]", order.Remark);
        Assert.Contains("张三", order.Remark);
    }

    [Fact]
    public void Void_Settled_Rejected()
    {
        var order = new Order { Id = 1, Status = OrderStatus.Settled, DeskId = 8 };
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L);
        var (ok, error, _) = vm.VoidOrder("想作废");
        Assert.False(ok);
        Assert.Contains("退单", error);   // 实现文案是「已结账订单不能冲正，请走退单流程」
        Assert.Equal(OrderStatus.Settled, order.Status); // 状态不变
    }

    [Fact]
    public void VoidConfirmViewModel_CanVoid_Reflects_Status()
    {
        var pending = new VoidConfirmViewModel(new Order { Status = OrderStatus.Pending, DeskId = 8 });
        Assert.True(pending.CanVoid);
        Assert.Contains("释放", pending.ImpactText); // 桌台释放是重点

        var settled = new VoidConfirmViewModel(new Order { Status = OrderStatus.Settled });
        Assert.False(settled.CanVoid);
        Assert.NotEmpty(settled.BlockReason);
    }

    [Fact]
    public void Void_AlreadyVoid_Rejected()
    {
        var order = new Order { Id = 1, Status = OrderStatus.Void };
        var vm = new RefundOrderViewModel(order, "张三", "店长", 10L);
        var (ok, _, _) = vm.VoidOrder("重复作废");
        Assert.False(ok);
    }
}
