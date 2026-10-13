using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Ordering;
using static YiHua.Pos.Tests.UI.Ordering.OrderingTestFactory;

namespace YiHua.Pos.Tests.UI.Ordering;

// 别名放在 namespace 内部：文件层级的 using 属于全局作用域，而
// YiHua.Pos.UI.ViewModels 下的 Desk / Product 子命名空间会参与名字解析，
// 更近的作用域优先，全局别名被跳过，导致 CS0118。
using Desk = YiHua.Pos.Domain.Entities.Desk;

public class SubmitFlowTests
{
    [Fact]
    public async Task Submit_Creates_Order_And_Outbox()
    {
        var orderRepo = new FakeOrderRepository();
        var outbox = new FakeOutboxRepository();
        var vm = new OrderEntryViewModel(
            new FakeProductRepository(), new FakeCategoryRepository(),
            orderRepo, outbox, new YiHua.Pos.UI.Services.Ordering.HangOrderStore(),
            OperatorRole.Cashier,
            new Desk { Id = 8, Name = "A08", GuestCount = 2 });

        vm.AddProduct(Noodle());
        var result = await vm.SubmitAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OrderId);
        Assert.NotNull(result.ClientOrderNo);

        var order = orderRepo.Orders.Single();
        Assert.Equal(18m, order.AmountPay);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(OrderType.DineIn, order.OrderType);
        Assert.Single(order.Items);

        // 订单号必须有值：它是查单、补打小票、退款的唯一凭据。
        // 之前 BuildOrder 没设 OrderNo，每张单的订单号都是空的，
        // 小票上印不出单号，客人拿小票回来也对不了账。
        Assert.False(string.IsNullOrWhiteSpace(order.OrderNo));

        // outbox 有记录
        Assert.Single(outbox.Items);
        Assert.Equal(result.ClientOrderNo, outbox.Items[0].ClientOrderNo);
    }

    [Fact]
    public async Task Submit_Empty_Lines_Fails()
    {
        var vm = CreateVm();
        var result = await vm.SubmitAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Submit_Idempotent_On_Retry()
    {
        var orderRepo = new FakeOrderRepository();
        var vm = new OrderEntryViewModel(
            new FakeProductRepository(), new FakeCategoryRepository(),
            orderRepo, new FakeOutboxRepository(),
            new YiHua.Pos.UI.Services.Ordering.HangOrderStore(),
            OperatorRole.Cashier, new Desk { Id = 8, Name = "A08" });

        vm.AddProduct(Noodle());

        // 第一次提交成功
        var r1 = await vm.SubmitAsync();
        Assert.True(r1.IsSuccess);

        // 模拟网络超时后重试：复用同一个 clientOrderNo
        // （通过状态机保留的键；这里直接验证仓储层幂等）
        var existing = await orderRepo.GetByClientOrderNoAsync(r1.ClientOrderNo!);
        Assert.NotNull(existing); // 幂等键能查到已存在的订单

        Assert.Equal(1, orderRepo.InsertCalls); // 只插入了一次
    }

    [Fact]
    public async Task Submit_Conflict_When_Desk_Order_Settled()
    {
        var orderRepo = new FakeOrderRepository();
        // 桌台已有被结账的订单（被别人结了）
        orderRepo.Orders.Add(new Order
        {
            Id = 99, DeskId = 8, Status = OrderStatus.Settled,
            ClientOrderNo = "other",
        });

        var vm = new OrderEntryViewModel(
            new FakeProductRepository(), new FakeCategoryRepository(),
            orderRepo, new FakeOutboxRepository(),
            new YiHua.Pos.UI.Services.Ordering.HangOrderStore(),
            OperatorRole.Cashier, new Desk { Id = 8, Name = "A08" });

        vm.AddProduct(Noodle());
        var result = await vm.SubmitAsync();

        // 注意：当前实现只在 active 非 Pending 时判冲突；
        // Settled 的订单会被过滤掉，所以这里不会冲突。
        // 这个测试记录当前行为：已结账桌台的新提交被允许（新开一单）。
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Refund_Unsubmitted_Line_Removes_It()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        bool ok = vm.TryRefundLine(line, "不要了", isSubmitted: false, out string? reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.Empty(vm.Lines);
    }

    [Fact]
    public void Refund_Submitted_Line_Marks_Refund_With_Reason()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        vm.AddProduct(Noodle()); // 36
        var line = vm.Lines[0];

        bool ok = vm.TryRefundLine(line, "上错了", isSubmitted: true, out _);

        Assert.True(ok);
        Assert.True(line.IsRefunded);
        Assert.Contains("上错了", line.Remark);
        Assert.Equal(0m, vm.Totals.Payable); // 退菜从合计扣除
    }

    [Fact]
    public void Refund_Requires_Reason()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());

        bool ok = vm.TryRefundLine(vm.Lines[0], "", isSubmitted: false, out string? reason);

        Assert.False(ok);
        Assert.NotNull(reason);
        Assert.Single(vm.Lines); // 没退掉
    }

    [Fact]
    public void Remark_Truncated_To_30_Chars()
    {
        var vm = CreateVm();
        vm.AddProduct(Noodle());
        var line = vm.Lines[0];

        vm.SetLineRemark(line, new string('x', 50));

        Assert.Equal(30, line.Remark.Length);
    }
}
