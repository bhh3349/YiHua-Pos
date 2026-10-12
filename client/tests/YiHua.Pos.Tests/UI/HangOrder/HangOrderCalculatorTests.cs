using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.HangOrder;

namespace YiHua.Pos.Tests.UI.HangOrder;

// ViewModels.Desk 命名空间已存在，用别名避免 CS0118
using Desk = YiHua.Pos.Domain.Entities.Desk;

public class HangOrderCalculatorTests
{
    private static Order SampleOrder()
    {
        var order = new Order
        {
            Id = 1, DeskId = 8, Status = OrderStatus.Pending,
            GuestCount = 4, AmountPay = 100m,
        };
        order.Items.Add(new OrderItem { Id = 101, ProductName = "牛肉面", Price = 18m, Qty = 2, Amount = 36m });
        order.Items.Add(new OrderItem { Id = 102, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, Amount = 38m });
        order.Items.Add(new OrderItem { Id = 103, ProductName = "可乐", Price = 8m, Qty = 1, Amount = 8m, IsGift = true });
        return order;
    }

    [Fact]
    public void RoundingMode_Is_AwayFromZero()
    {
        Assert.Equal(MidpointRounding.AwayFromZero, HangOrderCalculator.RoundingMode);
    }

    [Fact]
    public void ValidateSplit_Empty_Rejected()
    {
        var order = SampleOrder();
        Assert.False(HangOrderCalculator.ValidateSplit(order, Array.Empty<long>(), Array.Empty<long>(), out string? reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void ValidateSplit_Unknown_Item_Rejected()
    {
        var order = SampleOrder();
        Assert.False(HangOrderCalculator.ValidateSplit(order, new[] { 999L }, Array.Empty<long>(), out _));
    }

    [Fact]
    public void ValidateSplit_Already_Settled_Rejected()
    {
        var order = SampleOrder();
        var settled = new[] { 101L };
        Assert.False(HangOrderCalculator.ValidateSplit(order, new[] { 101L }, settled, out string? reason));
        Assert.Contains("已结", reason);
    }

    [Fact]
    public void ValidateSplit_Valid_Passes()
    {
        var order = SampleOrder();
        Assert.True(HangOrderCalculator.ValidateSplit(order, new[] { 101L }, Array.Empty<long>(), out _));
    }

    [Fact]
    public void CalcSplitAmount_Excludes_Gift()
    {
        var order = SampleOrder();
        // 103 是赠品，不计入
        decimal amount = HangOrderCalculator.CalcSplitAmount(new[] { order.Items[2] });
        Assert.Equal(0m, amount);

        decimal amount2 = HangOrderCalculator.CalcSplitAmount(new[] { order.Items[0], order.Items[1] });
        Assert.Equal(74m, amount2);
    }

    [Fact]
    public void CalcRemaining_Correct()
    {
        var order = SampleOrder();
        // 总 74（不含赠品），拆 36，剩 38
        decimal remaining = HangOrderCalculator.CalcRemaining(order, Array.Empty<long>(), new[] { 101L });
        Assert.Equal(38m, remaining);
    }

    [Fact]
    public void CalcStatusAfterSplit_Partial_Pending()
    {
        var order = SampleOrder();
        var status = HangOrderCalculator.CalcStatusAfterSplit(order, new[] { 101L });
        Assert.Equal(OrderStatus.Pending, status);
    }

    [Fact]
    public void CalcStatusAfterSplit_All_Settled()
    {
        var order = SampleOrder();
        var status = HangOrderCalculator.CalcStatusAfterSplit(order, new[] { 101L, 102L });
        Assert.Equal(OrderStatus.Settled, status);
    }

    [Fact]
    public void CalcStatusAfterSplit_Zero_Remaining_Settled()
    {
        var order = SampleOrder();
        // 全部有效明细结完（赠品不计）
        var status = HangOrderCalculator.CalcStatusAfterSplit(order, new[] { 101L, 102L });
        Assert.Equal(OrderStatus.Settled, status);
    }

    [Fact]
    public void IsTimedOut_Boundary()
    {
        var record = new HangOrderRecord { ParkTime = DateTime.Now.AddHours(-4) };
        // 刚好 4 小时 → 超时（含边界）
        Assert.True(HangOrderCalculator.IsTimedOut(record, TimeSpan.FromHours(4)));

        var fresh = new HangOrderRecord { ParkTime = DateTime.Now.AddHours(-3).AddMinutes(-59) };
        Assert.False(HangOrderCalculator.IsTimedOut(fresh, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void CanRecall_Original_Idle_Ok()
    {
        var record = new HangOrderRecord { DeskId = 8, DeskName = "A08" };
        var desk = new Desk { Id = 8, Name = "A08", Status = DeskStatus.Idle };
        Assert.True(HangOrderCalculator.CanRecall(record, desk, out _));
    }

    [Fact]
    public void CanRecall_Original_Occupied_Conflict()
    {
        var record = new HangOrderRecord { DeskId = 8, DeskName = "A08" };
        var desk = new Desk { Id = 8, Name = "A08", Status = DeskStatus.Occupied };
        Assert.False(HangOrderCalculator.CanRecall(record, desk, out string? reason));
        Assert.Contains("占用", reason);
        Assert.Contains("换", reason);
    }

    [Fact]
    public void CanRecall_ChangeDesk_Must_Be_Idle()
    {
        var record = new HangOrderRecord { DeskId = 8, DeskName = "A08" };
        var other = new Desk { Id = 9, Name = "A09", Status = DeskStatus.Idle };
        Assert.True(HangOrderCalculator.CanRecall(record, other, out _));

        var busy = new Desk { Id = 10, Name = "A10", Status = DeskStatus.Occupied };
        Assert.False(HangOrderCalculator.CanRecall(record, busy, out _));
    }
}
