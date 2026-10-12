using YiHua.Pos.UI.Services.Payment;
using YiHua.Pos.UI.ViewModels.Payment;
using Xunit;

namespace YiHua.Pos.Tests.UI.Payment;

/// <summary>
/// 提交状态机测试：防重/冲突/重试。
/// </summary>
public class PaymentSubmitStateMachineTests
{
    [Fact]
    public async Task Submit_Success_TransitionsToSucceeded()
    {
        var sm = new PaymentSubmitStateMachine();
        var result = await sm.SubmitAsync((ct) =>
            Task.FromResult((true, false, (string?)null)));
        Assert.True(result);
        Assert.Equal(PaymentSubmitState.Succeeded, sm.State);
    }

    [Fact]
    public async Task Submit_DoubleClick_SecondIgnored()
    {
        var sm = new PaymentSubmitStateMachine();
        var callCount = 0;
        var tcs = new TaskCompletionSource<(bool, bool, string?)>();

        // 第一次提交（挂起）
        var first = sm.SubmitAsync(async (ct) =>
        {
            callCount++;
            return await tcs.Task;
        });

        // 第二次提交（应被忽略，因为正在提交中）
        var second = await sm.SubmitAsync((ct) =>
            Task.FromResult((true, false, (string?)null)));

        Assert.False(second); // 被防重拦截
        Assert.Equal(1, callCount);

        tcs.SetResult((true, false, null));
        await first;
        Assert.Equal(PaymentSubmitState.Succeeded, sm.State);
    }

    [Fact]
    public async Task Submit_Conflict_SetsConflictState()
    {
        var sm = new PaymentSubmitStateMachine();
        var result = await sm.SubmitAsync((ct) =>
            Task.FromResult((false, true, "本单已在其他设备结账")));
        Assert.False(result);
        Assert.Equal(PaymentSubmitState.Conflicted, sm.State);
        Assert.Equal("本单已在其他设备结账", sm.ConflictMessage);
        // 冲突后不可重试提交（需返回）
        Assert.False(sm.CanSubmit);
    }

    [Fact]
    public async Task Submit_Failed_CanRetry()
    {
        var sm = new PaymentSubmitStateMachine();
        var result = await sm.SubmitAsync((ct) =>
            Task.FromResult((false, false, "网络超时")));
        Assert.False(result);
        Assert.Equal(PaymentSubmitState.Failed, sm.State);
        Assert.Equal("网络超时", sm.LastError);
        Assert.True(sm.CanSubmit); // 可重试

        // 重试成功
        result = await sm.SubmitAsync((ct) =>
            Task.FromResult((true, false, (string?)null)));
        Assert.True(result);
        Assert.Equal(PaymentSubmitState.Succeeded, sm.State);
    }

    [Fact]
    public async Task Submit_Exception_TreatedAsFailed()
    {
        var sm = new PaymentSubmitStateMachine();
        var result = await sm.SubmitAsync((ct) =>
            throw new InvalidOperationException("连接中断"));
        Assert.False(result);
        Assert.Equal(PaymentSubmitState.Failed, sm.State);
        Assert.Contains("连接中断", sm.LastError);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var sm = new PaymentSubmitStateMachine();
        sm.SubmitAsync((ct) => Task.FromResult((false, true, "冲突"))).Wait();
        sm.Reset();
        Assert.Equal(PaymentSubmitState.Idle, sm.State);
        Assert.Null(sm.ConflictMessage);
        Assert.True(sm.CanSubmit);
    }
}

/// <summary>
/// 打印重试测试：打印失败不回滚结账。
/// </summary>
public class PrintRetryTests
{
    private class FailPrinter : IReceiptPrinter
    {
        public int CallCount { get; private set; }
        public Task<bool> PrintAsync(ReceiptData data, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(false); // 总是失败
        }
    }

    private class OkPrinter : IReceiptPrinter
    {
        public Task<bool> PrintAsync(ReceiptData data, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private class FakeDisplay : ICustomerDisplayWriter
    {
        public Task ShowAmountsAsync(decimal total, decimal received, decimal change, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ShowWelcomeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeDrawer : ICashDrawerOpener
    {
        public bool Opened { get; private set; }
        public Task OpenAsync(CancellationToken ct = default) { Opened = true; return Task.CompletedTask; }
    }

    [Fact]
    public async Task PrintFailed_ReturnsFalse_DoesNotThrow()
    {
        var printer = new PaymentPrinter(new FailPrinter(), new FakeDisplay(), new FakeDrawer());
        var data = new ReceiptData { OrderNo = "ORD001", PayableAmount = 74m };
        // 打印失败不抛异常，返回 false（结账已成功，不回滚）
        var success = await printer.AfterPaymentAsync(data, isCash: true);
        Assert.False(success);
    }

    [Fact]
    public async Task CashPayment_OpensDrawer()
    {
        var drawer = new FakeDrawer();
        var printer = new PaymentPrinter(new OkPrinter(), new FakeDisplay(), drawer);
        var data = new ReceiptData { OrderNo = "ORD001" };
        await printer.AfterPaymentAsync(data, isCash: true);
        Assert.True(drawer.Opened);
    }

    [Fact]
    public async Task NonCash_DoesNotOpenDrawer()
    {
        var drawer = new FakeDrawer();
        var printer = new PaymentPrinter(new OkPrinter(), new FakeDisplay(), drawer);
        var data = new ReceiptData { OrderNo = "ORD001" };
        await printer.AfterPaymentAsync(data, isCash: false);
        Assert.False(drawer.Opened);
    }

    [Fact]
    public async Task PrintRetry_CanSucceedOnSecondAttempt()
    {
        var failPrinter = new FailPrinter();
        var printer = new PaymentPrinter(failPrinter, new FakeDisplay(), new FakeDrawer());
        var data = new ReceiptData { OrderNo = "ORD001" };

        Assert.False(await printer.AfterPaymentAsync(data, isCash: false));
        Assert.Equal(1, failPrinter.CallCount);

        // 重试（换一个成功的打印机）
        var retryPrinter = new PaymentPrinter(new OkPrinter(), new FakeDisplay(), new FakeDrawer());
        Assert.True(await retryPrinter.AfterPaymentAsync(data, isCash: false));
    }
}

/// <summary>
/// 网关测试：自定义收款可用，微信/支付宝显示暂未启用。
/// </summary>
public class PaymentGatewayTests
{
    [Fact]
    public async Task CustomQrGateway_ProcessesSuccessfully()
    {
        var gateway = new CustomQrPaymentGateway();
        Assert.True(gateway.IsEnabled);
        var result = await gateway.ProcessAsync(new PaymentRequest { OrderNo = "ORD001", Amount = 74m });
        Assert.True(result.Success);
        Assert.NotNull(result.TransactionId);
    }

    [Fact]
    public async Task WechatGateway_NotEnabled()
    {
        var gateway = new WechatPayGateway();
        Assert.False(gateway.IsEnabled);
        var result = await gateway.ProcessAsync(new PaymentRequest { OrderNo = "ORD001", Amount = 74m });
        Assert.False(result.Success);
        Assert.True(result.IsNotEnabled);
        Assert.Equal("暂未启用", result.Message);
    }

    [Fact]
    public async Task AlipayGateway_NotEnabled()
    {
        var gateway = new AlipayGateway();
        Assert.False(gateway.IsEnabled);
        var result = await gateway.ProcessAsync(new PaymentRequest { OrderNo = "ORD001", Amount = 74m });
        Assert.True(result.IsNotEnabled);
    }

    [Fact]
    public void GatewayFactory_ReturnsCorrectGateway()
    {
        Assert.IsType<CustomQrPaymentGateway>(PaymentGatewayFactory.Get("custom"));
        Assert.IsType<WechatPayGateway>(PaymentGatewayFactory.Get("wechat"));
        Assert.IsType<AlipayGateway>(PaymentGatewayFactory.Get("alipay"));
        Assert.Null(PaymentGatewayFactory.Get("unknown"));
    }

    [Fact]
    public void Discount_RequiresManager()
    {
        var vm = new PaymentViewModel();
        vm.Items.Add(new PaymentOrderItemDto { ProductName = "牛肉面", Amount = 74m });
        var request = new DiscountRequest { Type = DiscountType.Rate, Value = 0.9m, IsManager = false };
        Assert.Throws<UnauthorizedAccessException>(() => vm.ApplyDiscount(request));
    }

    [Fact]
    public void Discount_Manager_CanApply()
    {
        var vm = new PaymentViewModel();
        vm.Items.Add(new PaymentOrderItemDto { ProductName = "牛肉面", Amount = 74m });
        var request = new DiscountRequest { Type = DiscountType.Rate, Value = 0.9m, IsManager = true };
        vm.ApplyDiscount(request);
        Assert.Equal(66.60m, vm.PayableAmount);
        Assert.Equal("9.0折", vm.DiscountLabel);
    }
}
