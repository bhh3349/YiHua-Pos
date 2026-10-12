using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部：YiHua.Pos.Tests.UI.Payment 子命名空间会撞名（CS0118）
using PaymentEntity = YiHua.Pos.Domain.Entities.Payment;

/// <summary>计算规则测试：退菜/冲正/抹零/备用金不足</summary>
public class ShiftRulesTests
{
    [Fact]
    public void Refund_Cash_Reduces_CashExpected()
    {
        // 退菜退现金：现金应收减少
        var orders = new[]
        {
            new Order
            {
                Status = OrderStatus.Settled,
                AmountPay = 100m,
                AmountRefund = 30m,
                Payments = new List<PaymentEntity> { new() { PayType = PayType.Cash, Amount = 100m } },
            },
        };

        Assert.Equal(70m, ShiftCalculator.SumByPayType(orders, PayType.Cash));
    }

    [Fact]
    public void Reversal_Included_As_Negative()
    {
        // 冲正单：直接计入（负数冲减）
        var orders = new[]
        {
            new Order { Status = OrderStatus.Settled, AmountPay = 200m },
            new Order { Status = OrderStatus.Void, AmountPay = -56m },
        };

        Assert.Equal(144m, ShiftCalculator.NetAmount(orders));
    }

    [Fact]
    public void Discount_Includes_Moling()
    {
        // 抹零计入优惠：NetAmount 用 AmountPay（已扣减）
        var orders = new[]
        {
            new Order { Status = OrderStatus.Settled, AmountTotal = 100m, AmountDiscount = 3m, AmountPay = 97m },
        };

        Assert.Equal(3m, ShiftCalculator.TotalDiscount(orders));
        Assert.Equal(97m, ShiftCalculator.NetAmount(orders));
    }

    [Fact]
    public void Insufficient_Float_Leads_To_Shortage()
    {
        // 备用金不足：钱箱应有偏小，差额为负即短款
        decimal expected = ShiftCalculator.ExpectedCashInDrawer(
            openingFloat: 100m,  // 备用金只有100
            cashIncome: 5000m,
            cashRefund: 0m);
        decimal diff = ShiftCalculator.CalculateDifference(actualCash: 5050m, expectedCash: expected);

        Assert.Equal(5100m, expected);
        Assert.Equal(-50m, diff);
        Assert.Equal(DifferenceKind.Shortage, ShiftDifferencePolicy.Classify(diff));
    }
}
