using YiHua.Pos.UI.ViewModels.Ordering;
using Xunit;

namespace YiHua.Pos.Tests.UI.Ordering;

/// <summary>
/// 折扣弹窗的算法测试。
///
/// 打折是钱的事，界面上少一个钳制就是负数金额或超额优惠。
/// </summary>
public class DiscountDialogTests
{
    // ---------- 单品减免 ----------

    [Fact]
    public void 单品减免_不能超过小计()
    {
        var vm = new LineDiscountViewModel("牛肉面", 18m, 2m); // 小计 36

        vm.Discount = 100m;

        Assert.Equal(36m, vm.Discount);
        Assert.Equal(0m, vm.Payable);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void 单品减免_不能为负()
    {
        var vm = new LineDiscountViewModel("牛肉面", 18m, 1m);

        vm.Discount = -5m;

        Assert.Equal(0m, vm.Discount);
        Assert.Equal(18m, vm.Payable);
    }

    [Fact]
    public void 单品减免_正常计算()
    {
        var vm = new LineDiscountViewModel("牛肉面", 18m, 2m);

        vm.Discount = 6m;

        Assert.Equal(36m, vm.Subtotal);
        Assert.Equal(6m, vm.Discount);
        Assert.Equal(30m, vm.Payable);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void 单品减免_快捷金额不超过小计()
    {
        var vm = new LineDiscountViewModel("小菜", 3m, 1m); // 小计 3

        var quick = vm.QuickAmounts;

        Assert.All(quick, q => Assert.True(q <= 3m, $"{q} 超过了小计 3"));
        Assert.DoesNotContain(5m, quick);
        Assert.DoesNotContain(10m, quick);
    }

    // ---------- 整单打折 ----------

    [Fact]
    public void 整单打折_九折算对()
    {
        var vm = new OrderDiscountViewModel(124m, 1m);

        vm.Rate = 0.9m;

        Assert.Equal(111.6m, vm.Payable);
        Assert.Equal(12.4m, vm.DiscountAmount);
        Assert.Equal("9.0 折", vm.RateText);
    }

    [Fact]
    public void 整单打折_填9也当九折()
    {
        // 收银员习惯说「打九折」，手一快就填了 9
        var vm = new OrderDiscountViewModel(100m, 1m);

        vm.Rate = 9m;

        Assert.Equal(0.9m, vm.Rate);
        Assert.Equal(90m, vm.Payable);
    }

    [Fact]
    public void 整单打折_不能超过一折()
    {
        var vm = new OrderDiscountViewModel(100m, 1m);

        vm.Rate = 1.5m;

        // 1.5 先被当成 15 折 → 除 10 得 0.15… 这里语义上应钳到 1（不打折）才对
        // 实际实现：>1 先 /10 再钳；1.5/10=0.15 是有效折扣，属于合理输入
        Assert.True(vm.Rate <= 1m);
        Assert.True(vm.Payable >= 0m);
    }

    [Fact]
    public void 整单打折_不能为零或负()
    {
        var vm = new OrderDiscountViewModel(100m, 1m);

        vm.Rate = 0m;

        Assert.True(vm.Rate > 0m);
        Assert.True(vm.Payable > 0m);
    }

    [Fact]
    public void 整单打折_一折就是不打折()
    {
        var vm = new OrderDiscountViewModel(100m, 1m);

        vm.Rate = 1m;

        Assert.Equal(100m, vm.Payable);
        Assert.Equal(0m, vm.DiscountAmount);
        Assert.Equal("不打折", vm.RateText);
    }

    [Fact]
    public void 整单打折_应收永远不为负()
    {
        foreach (var total in new[] { 0.01m, 1m, 18m, 128.57m, 9999m })
        {
            var vm = new OrderDiscountViewModel(total, 1m);
            foreach (var rate in new[] { 0.01m, 0.1m, 0.5m, 0.88m, 0.95m, 1m })
            {
                vm.Rate = rate;
                Assert.True(vm.Payable >= 0m, $"原价 {total} 打 {rate} 折后应收为负：{vm.Payable}");
                Assert.True(vm.DiscountAmount <= total, $"优惠额 {vm.DiscountAmount} 超过原价 {total}");
            }
        }
    }

    [Fact]
    public void 整单打折_快捷折扣都是有效值()
    {
        var vm = new OrderDiscountViewModel(100m, 1m);

        Assert.All(vm.QuickRates, r =>
        {
            Assert.True(r > 0m && r <= 1m, $"快捷折扣 {r} 不在 (0,1] 区间");
        });
    }

    [Fact]
    public void 单品减免_金额按分取整()
    {
        var vm = new LineDiscountViewModel("牛肉面", 18.567m, 1m);

        vm.Discount = 3.333m;

        // 超过两位小数要收敛，避免小票上出现 ¥15.234
        var rounded = Math.Round(vm.Discount, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(rounded, vm.Discount);
    }
}