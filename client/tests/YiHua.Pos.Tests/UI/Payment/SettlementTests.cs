using YiHua.Pos.UI.Services.Payment;
using YiHua.Pos.UI.ViewModels.Payment;
using Xunit;

namespace YiHua.Pos.Tests.UI.Payment;

/// <summary>
/// 结账与整单优惠测试。
///
/// 守的核心不变量：**原价 - 优惠 = 应收**，以及实收不足不能结账。
/// 收银算错钱是事故，不是小 bug。
/// </summary>
public class SettlementTests
{
    private static PaymentViewModel VmWith(params (string Name, decimal Price, decimal Qty)[] items)
    {
        var vm = new PaymentViewModel { DeskInfo = "A01" };
        foreach (var (name, price, qty) in items)
            vm.Items.Add(new PaymentOrderItemDto
            {
                ProductName = name,
                Quantity = qty,
                Price = price,
                Amount = price * qty,
            });
        return vm;
    }

    private static DiscountRequest Manager(DiscountType type, decimal value = 0m)
        => new() { Type = type, Value = value, IsManager = true, OperatorName = "店长" };

    [Fact]
    public void 抹分_抹掉分保留到角()
    {
        var vm = VmWith(("牛肉面", 128.57m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.RoundToJiao));

        Assert.Equal(128.50m, vm.PayableAmount);
        Assert.Equal(0.07m, vm.DiscountAmount);
        Assert.Equal("抹分", vm.DiscountLabel);
    }

    [Fact]
    public void 抹角_抹掉角保留到元()
    {
        var vm = VmWith(("牛肉面", 128.50m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.RoundToYuan));

        Assert.Equal(128.00m, vm.PayableAmount);
        Assert.Equal(0.50m, vm.DiscountAmount);
        Assert.Equal("抹角", vm.DiscountLabel);
    }

    [Fact]
    public void 打折_九折()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Rate, 0.9m));

        Assert.Equal(90m, vm.PayableAmount);
        Assert.Equal(10m, vm.DiscountAmount);
        Assert.Equal("9.0折", vm.DiscountLabel);
    }

    [Fact]
    public void 减免_减固定金额()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Fixed, 15m));

        Assert.Equal(85m, vm.PayableAmount);
        Assert.Equal(15m, vm.DiscountAmount);
    }

    [Fact]
    public void 减免_不能超过原价()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Fixed, 999m));

        Assert.Equal(0m, vm.PayableAmount);
        Assert.Equal(100m, vm.DiscountAmount);
    }

    [Fact]
    public void 整单改价_直接指定应收()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Override, 66m));

        Assert.Equal(66m, vm.PayableAmount);
        Assert.Equal(34m, vm.DiscountAmount);
    }

    [Fact]
    public void 免单_实收为零()
    {
        var vm = VmWith(("牛肉面", 100m, 1m), ("奶茶", 12m, 2m));

        vm.ApplyDiscount(Manager(DiscountType.Free));

        Assert.Equal(0m, vm.PayableAmount);
        Assert.Equal(124m, vm.DiscountAmount);
        Assert.Equal("免单", vm.DiscountLabel);
    }

    [Fact]
    public void 再选一次优惠_是替换不是叠加()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Fixed, 10m));
        Assert.Equal(90m, vm.PayableAmount);

        // 改主意打九折：应该按原价 100 算，不是按 90 再打九折
        vm.ApplyDiscount(Manager(DiscountType.Rate, 0.9m));

        Assert.Equal(90m, vm.PayableAmount);
        Assert.Equal(10m, vm.DiscountAmount);
    }

    [Fact]
    public void 清除优惠_回到原价()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        vm.ApplyDiscount(Manager(DiscountType.Fixed, 30m));
        vm.ClearDiscount();

        Assert.Equal(100m, vm.PayableAmount);
        Assert.Equal(0m, vm.DiscountAmount);
        Assert.Null(vm.DiscountLabel);
    }

    [Fact]
    public void 非店长不能做整单优惠()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            vm.ApplyDiscount(new DiscountRequest { Type = DiscountType.Fixed, Value = 10m, IsManager = false }));

        Assert.Contains("店长", ex.Message);
        Assert.Equal(100m, vm.PayableAmount);
    }

    [Fact]
    public void 现金_实收不足不能结账()
    {
        var vm = VmWith(("牛肉面", 74m, 1m));
        vm.SelectMethod(PaymentMethodKind.Cash);
        vm.SetReceivedAmount(50m);

        var error = vm.Validate();

        Assert.NotNull(error);
        Assert.Contains("不足", error);
        Assert.True(vm.IsInsufficient);
    }

    [Fact]
    public void 现金_找零正确()
    {
        var vm = VmWith(("牛肉面", 74m, 1m));
        vm.SelectMethod(PaymentMethodKind.Cash);
        vm.SetReceivedAmount(100m);

        Assert.Null(vm.Validate());
        Assert.Equal(26m, vm.ChangeAmount);
        Assert.False(vm.IsInsufficient);
    }

    [Fact]
    public void 非现金_实收自动等于应收_不找零()
    {
        var vm = VmWith(("牛肉面", 74m, 1m));

        vm.SelectMethod(PaymentMethodKind.CustomQr);

        Assert.Equal(74m, vm.ReceivedAmount);
        Assert.Equal(0m, vm.ChangeAmount);
        Assert.Null(vm.Validate());
    }

    [Fact]
    public void 优惠后_实收要按新应收重算找零()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));
        vm.SelectMethod(PaymentMethodKind.Cash);
        vm.SetReceivedAmount(100m);
        Assert.Equal(0m, vm.ChangeAmount);

        // 抹分到 100.00 不变；改用九折 → 应收 90，找零应该变成 10
        vm.ApplyDiscount(Manager(DiscountType.Rate, 0.9m));

        Assert.Equal(90m, vm.PayableAmount);
        Assert.Equal(10m, vm.ChangeAmount);
    }

    [Fact]
    public void 正好按钮_实收等于应收()
    {
        var vm = VmWith(("牛肉面", 74.5m, 1m));
        vm.SelectMethod(PaymentMethodKind.Cash);

        vm.SetExactAmount();

        Assert.Equal(74.5m, vm.ReceivedAmount);
        Assert.Equal(0m, vm.ChangeAmount);
        Assert.Null(vm.Validate());
    }

    [Fact]
    public void 空订单不能结账()
    {
        var vm = new PaymentViewModel();

        Assert.Contains("没有明细", vm.Validate()!);
    }

    [Fact]
    public void 行级优惠类型_不能在整单优惠里用()
    {
        var vm = VmWith(("牛肉面", 100m, 1m));

        // 赠菜/菜品打折/菜品减免属于明细行，传进来应该报错而不是算错账
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.ApplyDiscount(Manager(DiscountType.Gift)));
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.ApplyDiscount(Manager(DiscountType.ItemRate, 0.9m)));
    }

    [Fact]
    public void 优惠类型编号_与参考端activityId一致()
    {
        // 报表按 activityId 分类统计，编号错了报表就串了
        Assert.Equal(1, (int)DiscountType.RoundToJiao);
        Assert.Equal(2, (int)DiscountType.RoundToYuan);
        Assert.Equal(3, (int)DiscountType.Override);
        Assert.Equal(4, (int)DiscountType.Fixed);
        Assert.Equal(5, (int)DiscountType.Rate);
        Assert.Equal(6, (int)DiscountType.Gift);
        Assert.Equal(7, (int)DiscountType.ItemRate);
        Assert.Equal(8, (int)DiscountType.ItemFixed);
        Assert.Equal(9, (int)DiscountType.Payment);
    }

    [Fact]
    public void 快捷金额_覆盖应收且从小到大()
    {
        var vm = VmWith(("牛肉面", 74m, 1m));

        var quick = vm.QuickAmounts;

        Assert.Contains(74m, quick);
        Assert.Equal(quick.OrderBy(x => x).ToList(), quick);
    }
}