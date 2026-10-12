using YiHua.Pos.UI.ViewModels.Payment;
using Xunit;

namespace YiHua.Pos.Tests.UI.Payment;

public class CashPaymentTests
{
    [Fact]
    public void Change_CalculatedCorrectly()
    {
        var vm = new CashPaymentViewModel { PayableAmount = 74m, ReceivedAmount = 100m };
        Assert.Equal(26m, vm.ChangeAmount);
        Assert.False(vm.IsInsufficient);
        Assert.Null(vm.Validate());
    }

    [Fact]
    public void Insufficient_Blocked()
    {
        var vm = new CashPaymentViewModel { PayableAmount = 74m, ReceivedAmount = 50m };
        Assert.True(vm.IsInsufficient);
        Assert.Equal(0m, vm.ChangeAmount); // 不足时找零为 0，不显示负数
        Assert.NotNull(vm.Validate());
        Assert.Contains("不足", vm.Validate());
    }

    [Fact]
    public void QuickAmount_SelectsCorrectly()
    {
        var vm = new CashPaymentViewModel { PayableAmount = 74m };
        vm.SelectQuickAmount(100m);
        Assert.Equal(100m, vm.ReceivedAmount);
        Assert.Equal(26m, vm.ChangeAmount);
    }

    [Fact]
    public void QuickAmounts_ContainsExpected()
    {
        var vm = new CashPaymentViewModel { PayableAmount = 74m };
        Assert.Contains(74m, vm.QuickAmounts);
        Assert.Contains(100m, vm.QuickAmounts);
    }
}

public class CreditPaymentTests
{
    [Fact]
    public void Validate_NoCustomer_Fails()
    {
        var vm = new CreditPaymentViewModel { PayableAmount = 100m };
        Assert.NotNull(vm.Validate());
    }

    [Fact]
    public void Validate_OverLimit_NonManager_Blocked()
    {
        var vm = new CreditPaymentViewModel
        {
            PayableAmount = 600m,
            CreditLimit = 500m,
            IsManager = false,
            SelectedCustomer = new CustomerDto { Name = "张三" },
        };
        var error = vm.Validate();
        Assert.NotNull(error);
        Assert.Contains("额度", error);
    }

    [Fact]
    public void Validate_OverLimit_Manager_Allowed()
    {
        var vm = new CreditPaymentViewModel
        {
            PayableAmount = 600m,
            CreditLimit = 500m,
            IsManager = true,
            SelectedCustomer = new CustomerDto { Name = "张三" },
        };
        Assert.Null(vm.Validate());
    }

    [Fact]
    public void Validate_WithinLimit_Passes()
    {
        var vm = new CreditPaymentViewModel
        {
            PayableAmount = 300m,
            CreditLimit = 500m,
            SelectedCustomer = new CustomerDto { Name = "李四", Phone = "13800000000" },
        };
        Assert.Null(vm.Validate());
    }
}

public class CustomQrPaymentTests
{
    [Fact]
    public void Validate_AmountMismatch_Blocked()
    {
        var vm = new CustomQrPaymentViewModel { PayableAmount = 74m, ReceivedAmount = 100m };
        var error = vm.Validate();
        Assert.NotNull(error);
        Assert.Contains("不找零", error);
    }

    [Fact]
    public void Validate_EqualAmount_Passes()
    {
        var vm = new CustomQrPaymentViewModel { PayableAmount = 74m, ReceivedAmount = 74m };
        Assert.Null(vm.Validate());
    }

    [Fact]
    public void BuildRequest_ContainsVoucherInfo()
    {
        var vm = new CustomQrPaymentViewModel
        {
            PayableAmount = 74m,
            ReceivedAmount = 74m,
            PayMethod = "支付宝",
            VoucherNo = "202410120001",
            Remark = "客人已扫码",
        };
        var req = vm.BuildRequest("ORD001");
        Assert.Equal("ORD001", req.OrderNo);
        Assert.Equal(74m, req.Amount);
        Assert.Equal("支付宝", req.Extra["pay_method"]);
        Assert.Equal("202410120001", req.Extra["voucher_no"]);
    }
}
