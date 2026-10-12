using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.HangOrder;

namespace YiHua.Pos.Tests.UI.HangOrder;

public class SplitOrderTests
{
    private static Order SampleOrder()
    {
        var order = new Order { Id = 1, DeskId = 8, Status = OrderStatus.Pending, AmountPay = 74m };
        order.Items.Add(new OrderItem { Id = 101, ProductName = "牛肉面", Price = 18m, Qty = 2, Amount = 36m });
        order.Items.Add(new OrderItem { Id = 102, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, Amount = 38m });
        return order;
    }

    [Fact]
    public void ToggleSelect_Updates_Amounts_Realtime()
    {
        var sm = new SplitStateMachine(SampleOrder());

        Assert.Equal(0m, sm.SplitAmount);
        Assert.Equal(74m, sm.RemainingAmount);

        sm.ToggleSelect(101);
        Assert.Equal(36m, sm.SplitAmount);
        Assert.Equal(38m, sm.RemainingAmount);
        Assert.True(sm.IsValid);

        sm.ToggleSelect(101); // 取消
        Assert.Equal(0m, sm.SplitAmount);
        Assert.False(sm.IsValid); // 选空 → 无效
    }

    [Fact]
    public void SelectAll_Splits_Everything()
    {
        var sm = new SplitStateMachine(SampleOrder());
        sm.SelectAll();

        Assert.Equal(74m, sm.SplitAmount);
        Assert.Equal(0m, sm.RemainingAmount);
        Assert.Equal(OrderStatus.Settled, sm.StatusAfterSplit);
        Assert.True(sm.IsValid);
    }

    [Fact]
    public void Confirm_Empty_Rejected()
    {
        var sm = new SplitStateMachine(SampleOrder());
        var ids = sm.Confirm(out string? reason);
        Assert.Empty(ids);
        Assert.NotNull(reason);
    }

    [Fact]
    public void Confirm_Valid_Returns_Ids()
    {
        var sm = new SplitStateMachine(SampleOrder());
        sm.ToggleSelect(101);
        var ids = sm.Confirm(out _);
        Assert.Single(ids);
        Assert.Equal(101L, ids[0]);
    }

    [Fact]
    public void AlreadySettled_Excluded_From_Available()
    {
        var sm = new SplitStateMachine(SampleOrder(), new[] { 101L });
        Assert.Single(sm.AvailableItems);
        Assert.Equal(102L, sm.AvailableItems[0].Id);

        // 尝试选已结的 → 无效
        sm.ToggleSelect(101);
        Assert.False(sm.IsValid);
    }

    [Fact]
    public void Multiple_Splits_Accumulate()
    {
        var order = SampleOrder();
        var settled = new List<long>();

        // 第一次：拆 101
        var sm1 = new SplitStateMachine(order, settled);
        sm1.ToggleSelect(101);
        settled.AddRange(sm1.Confirm(out _));
        Assert.Equal(OrderStatus.Pending, sm1.StatusAfterSplit);

        // 第二次：拆 102（基于已结累加）
        var sm2 = new SplitStateMachine(order, settled);
        Assert.Single(sm2.AvailableItems); // 只剩 102
        sm2.ToggleSelect(102);
        settled.AddRange(sm2.Confirm(out _));

        Assert.Equal(2, settled.Count);
        Assert.Equal(OrderStatus.Settled, sm2.StatusAfterSplit);
    }

    [Fact]
    public void SplitAmount_Never_Negative()
    {
        var sm = new SplitStateMachine(SampleOrder());
        sm.ToggleSelect(101);
        Assert.True(sm.SplitAmount >= 0);
        Assert.True(sm.RemainingAmount >= 0);
    }
}
