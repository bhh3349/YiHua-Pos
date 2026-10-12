using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Refund;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Refund;
using Xunit;

namespace YiHua.Pos.Tests.UI.Refund;

/// <summary>交班联动：退款计入当前班次，ShiftCalculator 能正确处理</summary>
public class RefundShiftTests
{
    [Fact]
    public void Refund_Recorded_In_Current_Shift()
    {
        // 原班次 5，当前班次 10 → 退款计入 10
        Assert.Equal(10L, RefundCalculator.ResolveShiftId(new Order(), 10L));
    }

    [Fact]
    public async Task RefundRecord_ShiftId_Is_Current()
    {
        var order = new Order { Id = 1, Status = OrderStatus.Settled, AmountReceived = 128m };
        var repo = new InMemoryRefundRepository();
        var vm = new RefundOrderViewModel(order, "张三", "收银员", 10L, repo);
        await vm.RefundOrderAsync(50m, "部分退", null);
        var records = await repo.GetByOrderIdAsync(1);
        Assert.Equal(10L, records[0].ShiftId); // 当前班，不是原班
    }

    [Fact]
    public void ShiftCalculator_Handles_Refund()
    {
        // T-309 的 ShiftCalculator：退菜退现金从现金应收扣减
        var orders = new[]
        {
            new Order
            {
                Status = OrderStatus.Settled,
                AmountPay = 128m,
                AmountRefund = 26m, // 退菜 26
                Payments = new List<YiHua.Pos.Domain.Entities.Payment>
                {
                    new() { PayType = PayType.Cash, Amount = 128m },
                },
            },
        };
        // 净额 = 128 - 26 = 102
        Assert.Equal(102m, ShiftCalculator.NetAmount(orders));
        // 退款统计
        var (count, amount) = ShiftCalculator.RefundStats(orders);
        Assert.Equal(1, count);
        Assert.Equal(26m, amount);
    }

    [Fact]
    public void Shift_NetAmount_After_Refund()
    {
        // 本班已结账 8600，退菜 88，退单 56 → 净额 8456
        var orders = new[]
        {
            new Order { Status = OrderStatus.Settled, AmountPay = 8600m, AmountRefund = 144m },
        };
        Assert.Equal(8456m, ShiftCalculator.NetAmount(orders));
    }
}
