using YiHua.Pos.UI.Services.Member;
using Xunit;

namespace YiHua.Pos.Tests.UI.Member;

public class MemberRefundTests
{
    private static MemberDto MakeMember(decimal balance) => new()
    {
        Id = 1, CardNo = "8800000001", Name = "张三",
        Phone = "13812345678", Balance = balance,
    };

    [Fact]
    public void Refund_Under100_CashAllowed()
    {
        var result = MemberRefundCalculator.Refund(
            MakeMember(80m),
            new MemberRefundRequest
            {
                Name = "张三", IdCardNo = "110101199001011234",
                RefundMethod = "现金", OperatorName = "老板",
            });
        Assert.True(result.Success);
        Assert.Equal(80m, result.RefundAmount);
        Assert.Equal("现金", result.RefundMethod);
        Assert.Equal(0m, result.Record!.BalanceAfter); // 清零
    }

    [Fact]
    public void Refund_Over100_RequiresMethod()
    {
        var result = MemberRefundCalculator.Refund(
            MakeMember(500m),
            new MemberRefundRequest
            {
                Name = "张三", IdCardNo = "110101199001011234",
                RefundMethod = "银行转账", OperatorName = "老板",
            });
        Assert.True(result.Success);
        Assert.Equal(500m, result.RefundAmount);
    }

    [Fact]
    public void Refund_MissingName_Rejected()
    {
        var result = MemberRefundCalculator.Refund(
            MakeMember(100m),
            new MemberRefundRequest { IdCardNo = "123", RefundMethod = "现金" });
        Assert.False(result.Success);
        Assert.Contains("姓名", result.Error);
    }

    [Fact]
    public void Refund_MissingIdCard_Rejected()
    {
        var result = MemberRefundCalculator.Refund(
            MakeMember(100m),
            new MemberRefundRequest { Name = "张三", RefundMethod = "现金" });
        Assert.False(result.Success);
        Assert.Contains("证件号", result.Error);
    }

    [Fact]
    public void Refund_ZeroBalance_Rejected()
    {
        var result = MemberRefundCalculator.Refund(
            MakeMember(0m),
            new MemberRefundRequest { Name = "张三", IdCardNo = "123", RefundMethod = "现金" });
        Assert.False(result.Success);
    }

    [Fact]
    public void MaskIdCard_Correct()
    {
        Assert.Equal("1101****1234", MemberRefundCalculator.MaskIdCard("110101199001011234"));
    }
}

public class CouponValidatorTests
{
    private static CouponDto MakeCoupon() => new()
    {
        Code = "C001", Name = "满100减20",
        Type = CouponType.FullReduction, Threshold = 100m, Value = 20m,
        Expiry = DateTime.Now.AddDays(7),
        BoundMemberId = 1,
    };

    [Fact]
    public void ValidateThreshold_Below_Rejected()
    {
        Assert.False(CouponValidator.ValidateThreshold(80m, 100m, out var error));
        Assert.Contains("门槛", error);
    }

    [Fact]
    public void ValidateThreshold_Meets_Passes()
    {
        Assert.True(CouponValidator.ValidateThreshold(100m, 100m, out _));
        Assert.True(CouponValidator.ValidateThreshold(150m, 100m, out _));
    }

    [Fact]
    public void ValidateExpiry_Expired_Rejected()
    {
        Assert.False(CouponValidator.ValidateExpiry(
            DateTime.Now.AddDays(-1), DateTime.Now, out var error));
        Assert.Contains("过期", error);
    }

    [Fact]
    public void ValidateRedeemable_Used_Rejected()
    {
        var coupon = MakeCoupon();
        coupon.IsUsed = true;
        Assert.False(CouponValidator.ValidateRedeemable(coupon, 1, out var error));
        Assert.Contains("用过", error);
    }

    [Fact]
    public void ValidateRedeemable_WrongMember_Rejected()
    {
        var coupon = MakeCoupon(); // 绑定会员 1
        Assert.False(CouponValidator.ValidateRedeemable(coupon, 2, out var error));
        Assert.Contains("不是你的", error);
    }

    [Fact]
    public void Validate_AllPass()
    {
        var coupon = MakeCoupon();
        Assert.True(CouponValidator.Validate(coupon, 150m, 1, DateTime.Now, out _));
    }
}

public class CouponIdempotentTests
{
    [Fact]
    public async Task Redeem_Twice_OnlyFirstSucceeds()
    {
        var repo = new FakeCouponRepository();
        repo.Seed(new CouponDto
        {
            Code = "C001", Name = "满100减20",
            Type = CouponType.FullReduction, Threshold = 100m, Value = 20m,
            Expiry = DateTime.Now.AddDays(7), BoundMemberId = 1,
        });

        var first = await repo.RedeemAsync("C001", 1);
        var second = await repo.RedeemAsync("C001", 1);

        Assert.True(first);   // 第一次成功
        Assert.False(second); // 第二次失败（幂等）
    }

    [Fact]
    public async Task Redeem_WrongMember_Fails()
    {
        var repo = new FakeCouponRepository();
        repo.Seed(new CouponDto
        {
            Code = "C001", BoundMemberId = 1,
            Expiry = DateTime.Now.AddDays(7),
        });

        Assert.False(await repo.RedeemAsync("C001", 2));
    }
}

public class CouponCalculatorTests
{
    [Fact]
    public void CalcDiscount_FullReduction()
    {
        var coupon = new CouponDto { Type = CouponType.FullReduction, Value = 20m };
        Assert.Equal(20m, CouponCalculator.CalcDiscount(coupon, 150m));
    }

    [Fact]
    public void CalcDiscount_Discount()
    {
        var coupon = new CouponDto { Type = CouponType.Discount, Value = 0.9m };
        Assert.Equal(15m, CouponCalculator.CalcDiscount(coupon, 150m)); // 150*0.1
    }

    [Fact]
    public void CalcDiscount_Voucher_Capped()
    {
        var coupon = new CouponDto { Type = CouponType.Voucher, Value = 50m };
        Assert.Equal(30m, CouponCalculator.CalcDiscount(coupon, 30m)); // 不超过订单金额
    }
}
