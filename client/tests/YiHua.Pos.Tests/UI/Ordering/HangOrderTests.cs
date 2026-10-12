using Xunit;
using YiHua.Pos.UI.Services.Ordering;
using YiHua.Pos.UI.ViewModels.Ordering;
using static YiHua.Pos.Tests.UI.Ordering.OrderingTestFactory;

namespace YiHua.Pos.Tests.UI.Ordering;

public class HangOrderTests
{
    [Fact]
    public void Hang_Clears_Current_And_Stores_Snapshot()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        vm.AddProduct(Noodle());

        string hangId = vm.HangCurrentOrder();

        Assert.NotEmpty(hangId);
        Assert.Empty(vm.Lines); // 界面清空
        Assert.Equal(1, vm.HangStore.Count);
    }

    [Fact]
    public void Restore_Brings_Back_Lines_Intact()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        vm.AddProduct(Noodle());
        string hangId = vm.HangCurrentOrder();

        Assert.True(vm.RestoreHangOrder(hangId));

        Assert.Single(vm.Lines);
        Assert.Equal(2m, vm.Lines[0].Qty);
        Assert.Equal(36m, vm.Totals.Payable);
        Assert.Equal(0, vm.HangStore.Count); // 取单后移除
    }

    [Fact]
    public void Restore_Unknown_Id_Returns_False()
    {
        var vm = CreateVm();
        Assert.False(vm.RestoreHangOrder("nope"));
    }

    [Fact]
    public void HangOrder_Isolated_From_Real_Orders()
    {
        // 挂单只进 HangOrderStore，不写订单仓储
        var orderRepo = new FakeOrderRepository();
        var vm = CreateVm(orderRepo: orderRepo);
        vm.AddProduct(Noodle());
        vm.HangCurrentOrder();

        Assert.Empty(orderRepo.Orders);
        Assert.Equal(1, vm.HangStore.Count);
    }

    [Fact]
    public void Stale_HangOrder_Flagged_After_30min()
    {
        var store = new HangOrderStore();
        var old = new HangOrderSnapshot { HangTime = DateTime.Now.AddMinutes(-31) };
        var fresh = new HangOrderSnapshot { HangTime = DateTime.Now.AddMinutes(-5) };
        store.Hang(old);
        store.Hang(fresh);

        Assert.True(store.Peek(old.HangId)!.IsStale);
        Assert.False(store.Peek(fresh.HangId)!.IsStale);
    }

    [Fact]
    public void HangOrder_Amount_Calculated()
    {
        var snapshot = new HangOrderSnapshot
        {
            Lines = new List<OrderLineInput>
            {
                new() { Qty = 2, Price = 18m },
            },
        };
        Assert.Equal(36m, snapshot.Amount);
    }
}
