using Xunit;
using YiHua.Pos.UI.Services.Ordering;
using YiHua.Pos.UI.ViewModels.Ordering;
using static YiHua.Pos.Tests.UI.Ordering.OrderingTestFactory;

namespace YiHua.Pos.Tests.UI.Ordering;

public class WeightTests
{
    [Fact]
    public void WeightInput_Amount_Calculated_Not_Typed()
    {
        var vm = new WeightInputViewModel("海鲜拼盘", 68m);
        vm.WeightKg = 1.25m;

        // 金额是算出来的：1.25 × 68 = 85.00
        Assert.Equal(85.00m, vm.Amount);
    }

    [Fact]
    public void WeightInput_Rejects_Zero_Weight()
    {
        var vm = new WeightInputViewModel("海鲜拼盘", 68m);
        vm.WeightKg = 0m;

        Assert.False(vm.TryConfirm(out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void WeightInput_Accepts_Scale_Reading()
    {
        var vm = new WeightInputViewModel("海鲜拼盘", 68m);
        vm.ApplyScaleWeight(2.5m); // 电子秤读数接入

        Assert.Equal(2.5m, vm.WeightKg);
        Assert.Equal(170.00m, vm.Amount);
        Assert.True(vm.TryConfirm(out _));
    }

    [Fact]
    public void Weighted_Line_Amount_Uses_Weight()
    {
        var vm = CreateVm();
        var line = new OrderLineViewModel
        {
            ProductId = 3,
            ProductName = "海鲜拼盘",
            Price = 68m,
            IsWeighted = true,
            WeightKg = 1.25m,
        };
        vm.AddLine(line);

        Assert.Equal(85.00m, line.Amount);
        Assert.Equal("1.25kg", line.DisplayQty);
    }

    [Fact]
    public void Weighted_Line_Does_Not_Use_QtyStepper()
    {
        var vm = CreateVm();
        var line = new OrderLineViewModel
        {
            ProductId = 3, ProductName = "海鲜拼盘", Price = 68m,
            IsWeighted = true, WeightKg = 1m,
        };
        vm.AddLine(line);

        decimal before = line.WeightKg;
        vm.IncrementLine(line); // 步进器对秤重商品无效
        vm.DecrementLine(line);

        Assert.Equal(before, line.WeightKg);
    }

    [Fact]
    public void Calculator_Weighted_Rounding()
    {
        // 0.335kg × 100 = 33.50（AwayFromZero）
        Assert.Equal(33.50m, OrderCalculator.CalcWeightedAmount(0.335m, 100m));
    }
}
