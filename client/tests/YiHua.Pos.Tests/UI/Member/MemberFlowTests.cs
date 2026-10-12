using YiHua.Pos.UI.Services.Member;
using Xunit;

namespace YiHua.Pos.Tests.UI.Member;

/// <summary>
/// 完整流程测试：开卡 → 充值 → 消费 → 余额调整 → 退卡。
/// </summary>
public class MemberFlowTests
{
    [Fact]
    public async Task FullFlow_CreateRechargeConsumeRefund()
    {
        var memberRepo = new FakeMemberRepository();
        var couponRepo = new FakeCouponRepository();

        // 1. 开卡
        var member = await memberRepo.CreateAsync(new MemberDto
        {
            Name = "张三", Phone = "13812345678",
        });
        Assert.Equal("8800000001", member.CardNo);
        Assert.Equal(0m, member.Balance);

        // 2. 充值 1000
        Assert.True(RechargeValidator.Validate(1000m, 5000m, member.Balance, out _));
        member.Balance = 1000m;
        await memberRepo.UpdateAsync(member);
        await memberRepo.AddBalanceRecordAsync(new BalanceRecordDto
        {
            MemberId = member.Id, Amount = 1000m, BalanceAfter = 1000m,
            Reason = BalanceChangeReason.Recharge, ReasonText = "充值",
            OperatorName = "老板",
        });

        // 3. 余额调整 +10（补偿）
        var adjust = BalanceAdjuster.Adjust(member, 10m, BalanceChangeReason.Compensation, "老板");
        Assert.True(adjust.Success);
        member.Balance = adjust.NewBalance;
        await memberRepo.UpdateAsync(member);
        await memberRepo.AddBalanceRecordAsync(adjust.Record!);
        Assert.Equal(1010m, member.Balance);

        // 4. 查流水：能查清每次变动
        var records = await memberRepo.GetBalanceRecordsAsync(member.Id);
        Assert.Equal(2, records.Count);

        // 5. 会员价消费
        var items = new List<MemberOrderItem>
        {
            new() { ProductName = "牛肉面", Price = 22m, MemberPrice = 18m, Qty = 2 },
        };
        var discount = MemberPriceCalculator.Calc(items, member, 0.9m, null, null);
        Assert.Equal(36m, discount.Payable); // 会员价优先

        // 6. 退卡（余额 1010 ≥ 100，需记录退款方式）
        var refund = MemberRefundCalculator.Refund(member, new MemberRefundRequest
        {
            Name = "张三", IdCardNo = "110101199001011234",
            RefundMethod = "银行转账", OperatorName = "老板",
        });
        Assert.True(refund.Success);
        Assert.Equal(1010m, refund.RefundAmount);
    }

    [Fact]
    public async Task Flow_PhoneDuplicate_Rejected()
    {
        var repo = new FakeMemberRepository();
        await repo.CreateAsync(new MemberDto { Name = "张三", Phone = "13812345678" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.CreateAsync(new MemberDto { Name = "李四", Phone = "13812345678" }));
        Assert.Contains("已开过卡", ex.Message);
    }

    [Fact]
    public async Task Flow_DisabledMember_HistoryPreserved()
    {
        var repo = new FakeMemberRepository();
        var member = await repo.CreateAsync(new MemberDto
        {
            Name = "王五", Phone = "13912345678", Balance = 200m,
        });
        await repo.AddBalanceRecordAsync(new BalanceRecordDto
        {
            MemberId = member.Id, Amount = 200m, BalanceAfter = 200m,
            Reason = BalanceChangeReason.Recharge, ReasonText = "充值",
            OperatorName = "老板",
        });

        // 停用
        member.IsDisabled = true;
        await repo.UpdateAsync(member);

        // 历史仍可查
        var found = await repo.GetByIdAsync(member.Id);
        Assert.NotNull(found);
        Assert.True(found.IsDisabled);
        var records = await repo.GetBalanceRecordsAsync(member.Id);
        Assert.Single(records); // 流水保留
    }

    [Fact]
    public async Task Flow_CouponLifecycle()
    {
        var repo = new FakeCouponRepository();
        var coupon = new CouponDto
        {
            Code = "NEW100", Name = "新人券",
            Type = CouponType.FullReduction, Threshold = 50m, Value = 10m,
            Expiry = DateTime.Now.AddDays(30), BoundMemberId = 1,
        };
        await repo.CreateAsync(coupon);

        // 校验通过
        Assert.True(CouponValidator.Validate(coupon, 80m, 1, DateTime.Now, out _));

        // 核销
        Assert.True(await repo.RedeemAsync("NEW100", 1));

        // 抵扣计算
        Assert.Equal(10m, CouponCalculator.CalcDiscount(coupon, 80m));
    }
}
