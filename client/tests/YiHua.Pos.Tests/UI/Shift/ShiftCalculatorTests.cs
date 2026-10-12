using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部：YiHua.Pos.Tests.UI.Payment 子命名空间会撞名（CS0118）
using PaymentEntity = YiHua.Pos.Domain.Entities.Payment;

/// <summary>ShiftCalculator 测试：钱箱应有/差额/各支付方式汇总</summary>
public class ShiftCalculatorTests
{
    private static Order MakeOrder(decimal cashPay, decimal refund = 0, bool settled = true) =>
        new()
        {
            Status = settled ? OrderStatus.Settled : OrderStatus.Pending,
            AmountPay = cashPay,
            AmountRefund = refund,
            Payments = new List<PaymentEntity> { new() { PayType = PayType.Cash, Amount = cashPay } },
        };

    [Fact]
    public void SumByPayType_Cash_Deducts_Refund()
    {
        var orders = new[] { MakeOrder(100, refund: 20), MakeOrder(50) };

        decimal cash = ShiftCalculator.SumByPayType(orders, PayType.Cash);

        Assert.Equal(130m, cash); // (100-20) + 50
    }

    [Fact]
    public void SumByPayType_Ignores_Pending()
    {
        var orders = new[] { MakeOrder(100, settled: false) };

        Assert.Equal(0m, ShiftCalculator.SumByPayType(orders, PayType.Cash));
    }

    [Fact]
    public void ExpectedCashInDrawer_Formula()
    {
        // 备用金500 + 现金收入5200 - 现金退款100 = 5600
        decimal expected = ShiftCalculator.ExpectedCashInDrawer(500m, 5200m, 100m);

        Assert.Equal(5600m, expected);
    }

    [Fact]
    public void CalculateDifference_Shortage_Negative()
    {
        // 实点 5450，应有 5600 → 短款 -150
        decimal diff = ShiftCalculator.CalculateDifference(5450m, 5600m);

        Assert.Equal(-150m, diff);
    }

    [Fact]
    public void CalculateDifference_Overage_Positive()
    {
        decimal diff = ShiftCalculator.CalculateDifference(5750m, 5600m);

        Assert.Equal(150m, diff);
    }

    [Fact]
    public void NetAmount_Includes_Reversal_Negative()
    {
        var orders = new[]
        {
            new Order { Status = OrderStatus.Settled, AmountPay = 128m },
            new Order { Status = OrderStatus.Void, AmountPay = -128m }, // 冲正单
        };

        Assert.Equal(0m, ShiftCalculator.NetAmount(orders));
    }

    [Fact]
    public void Rounding_AwayFromZero()
    {
        // 2.5 → 3（AwayFromZero，而非银行家舍入的 2）
        var orders = new[]
        {
            new Order
            {
                Status = OrderStatus.Settled,
                AmountPay = 2.555m,
                Payments = new List<PaymentEntity> { new() { PayType = PayType.Cash, Amount = 2.555m } },
            },
        };

        Assert.Equal(2.56m, ShiftCalculator.SumByPayType(orders, PayType.Cash));
    }
}
