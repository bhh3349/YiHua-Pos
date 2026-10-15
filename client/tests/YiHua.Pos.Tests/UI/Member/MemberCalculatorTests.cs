using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.ViewModels.Member;
using Xunit;
using MemberEntity = YiHua.Pos.Domain.Entities.Member;

namespace YiHua.Pos.Tests.UI.Member;

/// <summary>
/// 会员金额与积分逻辑测试。
///
/// 会员余额是客人的钱，所以守这几条：
///   · 充值金额必须为正
///   · 手工调整必须填原因（事后要能说清）
///   · 余额/积分不能变成负数
///   · 消费不能超余额（不允许赊账）
///   · 注销时余额必须退掉，不能带着钱注销
/// </summary>
public class MemberCalculatorTests
{
    private static MemberEntity Card(decimal balance = 0m, int points = 0)
        => new()
        {
            Id = 7, CardNo = "M000001", Name = "张三", Phone = "13800138000",
            Balance = balance, Points = points,
        };

    // ---------- 充值 ----------

    [Fact]
    public void 充值_余额增加()
    {
        var member = Card(balance: 50m);

        var (after, tx) = MemberCalculator.Recharge(member, 100m, "现金", "店长", 1);

        Assert.Equal(150m, after);
        Assert.Equal(100m, tx.Amount);
        Assert.Equal(MemberTransactionType.Recharge, tx.Type);
        Assert.Equal(150m, tx.BalanceAfter);
        Assert.Equal("现金", tx.PayTypeName);
    }

    [Fact]
    public void 充值_金额必须为正()
    {
        var member = Card();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MemberCalculator.Recharge(member, 0m, "现金", "店长", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MemberCalculator.Recharge(member, -50m, "现金", "店长", 1));
    }

    [Fact]
    public void 充值_金额按分取整()
    {
        var member = Card();

        var (after, _) = MemberCalculator.Recharge(member, 100.005m, "现金", "店长", 1);

        Assert.Equal(decimal.Round(after, 2, MidpointRounding.AwayFromZero), after);
    }

    // ---------- 余额调整 ----------

    [Fact]
    public void 余额调整_必须填原因()
    {
        var member = Card(balance: 100m);

        var (ok, error, after, tx) = MemberCalculator.AdjustBalance(
            member, 80m, "  ", "店长", 1);

        Assert.False(ok);
        Assert.Contains("原因", error);
        Assert.Equal(100m, after);   // 原值不动
        Assert.Null(tx);
    }

    [Fact]
    public void 余额调整_不能为负()
    {
        var member = Card(balance: 100m);

        var (ok, error, _, _) = MemberCalculator.AdjustBalance(member, -10m, "手滑", "店长", 1);

        Assert.False(ok);
        Assert.Contains("负数", error);
    }

    [Fact]
    public void 余额调整_值没变就拒绝()
    {
        var member = Card(balance: 100m);

        var (ok, error, _, _) = MemberCalculator.AdjustBalance(member, 100m, "没事找事", "店长", 1);

        Assert.False(ok);
        Assert.Contains("一样", error);
    }

    [Fact]
    public void 余额调整_记录差额和原因()
    {
        var member = Card(balance: 100m);

        var (ok, _, after, tx) = MemberCalculator.AdjustBalance(member, 80m, "上月多扣", "店长", 1);

        Assert.True(ok);
        Assert.Equal(80m, after);
        Assert.Equal(-20m, tx!.Amount);          // 差额记在流水上
        Assert.Equal(80m, tx.BalanceAfter);      // 变动后余额
        Assert.Equal("上月多扣", tx.Reason);
        Assert.Equal(MemberTransactionType.BalanceAdjust, tx.Type);
    }

    // ---------- 积分调整 ----------

    [Fact]
    public void 积分调整_必须填原因()
    {
        var member = Card(points: 100);

        var (ok, error, _, _) = MemberCalculator.AdjustPoints(member, 50, "", "店长", 1);

        Assert.False(ok);
        Assert.Contains("原因", error);
    }

    [Fact]
    public void 积分调整_记录积分差额()
    {
        var member = Card(points: 100);

        var (ok, _, after, tx) = MemberCalculator.AdjustPoints(member, 60, "积分换购失败回退", "店长", 1);

        Assert.True(ok);
        Assert.Equal(60, after);
        Assert.Equal(-40, tx!.PointsChange);
        Assert.Equal(0m, tx.Amount);   // 积分调整不动钱
        Assert.Equal(60, tx.PointsAfter);
    }

    [Fact]
    public void 积分调整_不能为负()
    {
        var member = Card(points: 100);

        var (ok, error, _, _) = MemberCalculator.AdjustPoints(member, -1, "测试", "店长", 1);

        Assert.False(ok);
        Assert.Contains("负数", error);
    }

    // ---------- 消费 ----------

    [Fact]
    public void 消费_余额不足要拒绝()
    {
        var member = Card(balance: 30m);

        var (ok, error, after, tx) = MemberCalculator.Consume(member, 50m, "L001", "店长", 1);

        Assert.False(ok);
        Assert.Contains("余额不足", error);
        Assert.Equal(30m, after);
        Assert.Null(tx);
    }

    [Fact]
    public void 消费_扣减余额并记负数流水()
    {
        var member = Card(balance: 100m);

        var (ok, _, after, tx) = MemberCalculator.Consume(member, 30m, "L001", "店长", 1);

        Assert.True(ok);
        Assert.Equal(70m, after);
        Assert.Equal(-30m, tx!.Amount);
        Assert.Equal("L001", tx.OrderNo);
        Assert.Equal(MemberTransactionType.Consume, tx.Type);
    }

    [Fact]
    public void 消费_正好扣完余额可以()
    {
        var member = Card(balance: 30m);

        var (ok, _, after, _) = MemberCalculator.Consume(member, 30m, "L001", "店长", 1);

        Assert.True(ok);
        Assert.Equal(0m, after);
    }

    // ---------- 注销退款 ----------

    [Fact]
    public void 注销_有余额要退掉()
    {
        var member = Card(balance: 88.5m);

        var (ok, _, after, tx) = MemberCalculator.CancelRefund(member, "现金", "店长", 1);

        Assert.True(ok);
        Assert.Equal(0m, after);
        Assert.NotNull(tx);
        Assert.Equal(-88.5m, tx!.Amount);
        Assert.Equal(MemberTransactionType.CancelRefund, tx.Type);
    }

    [Fact]
    public void 注销_没余额就不产生流水()
    {
        var member = Card(balance: 0m);

        var (ok, _, after, tx) = MemberCalculator.CancelRefund(member, "现金", "店长", 1);

        Assert.True(ok);
        Assert.Equal(0m, after);
        Assert.Null(tx);   // 没动钱就不用记流水
    }

    // ---------- 资料校验 ----------

    [Fact]
    public void 资料校验_卡号不能空()
    {
        var (ok, error) = MemberCalculator.ValidateProfile("张三", "  ", "", false);

        Assert.False(ok);
        Assert.Contains("卡号", error);
    }

    [Fact]
    public void 资料校验_卡号不能重复()
    {
        var (ok, error) = MemberCalculator.ValidateProfile("张三", "M000001", "", cardNoTaken: true);

        Assert.False(ok);
        Assert.Contains("已经被用", error);
    }

    [Fact]
    public void 资料校验_姓名不能空()
    {
        var (ok, error) = MemberCalculator.ValidateProfile("", "M000002", "", false);

        Assert.False(ok);
        Assert.Contains("姓名", error);
    }

    [Fact]
    public void 资料校验_手机号可不填()
    {
        var (ok, _) = MemberCalculator.ValidateProfile("张三", "M000003", "", false);

        Assert.True(ok);
    }

    [Fact]
    public void 资料校验_手机号要11位数字()
    {
        Assert.False(MemberCalculator.ValidateProfile("张三", "M1", "138", false).Ok);
        Assert.False(MemberCalculator.ValidateProfile("张三", "M1", "1380013800a", false).Ok);
        Assert.True(MemberCalculator.ValidateProfile("张三", "M1", "13800138000", false).Ok);
    }

    // ---------- 手机号脱敏 ----------

    [Fact]
    public void 手机号脱敏_收银台不显示完整号()
    {
        var member = Card();
        member.Phone = "13800138000";

        Assert.Equal("138****8000", member.MaskedPhone);
    }

    [Fact]
    public void 手机号脱敏_非11位原样返回()
    {
        var member = Card();
        member.Phone = "123";

        Assert.Equal("123", member.MaskedPhone);
    }
}