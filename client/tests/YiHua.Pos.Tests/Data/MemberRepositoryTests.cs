using Xunit;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.ViewModels.Member;

namespace YiHua.Pos.Tests.Data;

/// <summary>
/// 会员仓储（真实 SQLite）。
///
/// 守的核心：**写流水和改余额在同一个事务里**。
/// 分开写的话，中间任何一步失败都会让余额和流水对不上，
/// 而会员余额是客人的钱，对不上就是纠纷。
/// </summary>
public class MemberRepositoryTests : TempDbFixture
{
    private MemberRepository Repo => new(Ctx.Db);

    private async Task<long> NewMemberAsync(string cardNo = "M000001", decimal balance = 0m, int points = 0)
        => await Repo.SaveAsync(new Member
        {
            CardNo = cardNo,
            Name = "张三",
            Phone = "13800138000",
            Balance = balance,
            Points = points,
        });

    [Fact]
    public async Task 建卡_能按卡号查回来()
    {
        await NewMemberAsync("M000001");

        var found = await Repo.GetByCardNoAsync("M000001");

        Assert.NotNull(found);
        Assert.Equal("张三", found!.Name);
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task 卡号查重()
    {
        var id = await NewMemberAsync("M000001");

        Assert.True(await Repo.CardNoExistsAsync("M000001"));
        Assert.False(await Repo.CardNoExistsAsync("M000001", excludeId: id)); // 排除自己
        Assert.False(await Repo.CardNoExistsAsync("M000002"));
    }

    [Fact]
    public async Task 自动生成卡号_递增且不复用()
    {
        await NewMemberAsync("M000001");
        await NewMemberAsync("M000002");

        var next = await Repo.NextCardNoAsync();

        Assert.Equal("M000003", next);
    }

    [Fact]
    public async Task 自动生成卡号_跳过已注销的号()
    {
        var id = await NewMemberAsync("M000005");
        await Repo.SetDataStatusAsync(id, 99);

        var next = await Repo.NextCardNoAsync();

        // 注销的号要留档，不能复用
        Assert.Equal("M000006", next);
    }

    [Fact]
    public async Task 充值_余额和流水一起落库()
    {
        var id = await NewMemberAsync(balance: 50m);
        var member = (await Repo.GetByIdAsync(id))!;

        var (after, tx) = MemberCalculator.Recharge(member, 100m, "现金", "店长", 1);
        await Repo.AddTransactionAsync(tx);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal(150m, reloaded.Balance);

        var txs = await Repo.GetTransactionsAsync(id);
        Assert.Single(txs);
        Assert.Equal(100m, txs[0].Amount);
        Assert.Equal(150m, txs[0].BalanceAfter);
    }

    [Fact]
    public async Task 多次操作_余额与最后一笔流水的变动后余额一致()
    {
        var id = await NewMemberAsync();

        foreach (var amount in new[] { 100m, 50m, 30m })
        {
            var member = (await Repo.GetByIdAsync(id))!;
            var (_, tx) = MemberCalculator.Recharge(member, amount, "现金", "店长", 1);
            await Repo.AddTransactionAsync(tx);
        }

        var final = (await Repo.GetByIdAsync(id))!;
        var txs = await Repo.GetTransactionsAsync(id);

        Assert.Equal(180m, final.Balance);
        Assert.Equal(3, txs.Count);
        Assert.Equal(final.Balance, txs[0].BalanceAfter);   // 倒序，第一条是最新
    }

    [Fact]
    public async Task 余额调整_记录原因和差额()
    {
        var id = await NewMemberAsync(balance: 100m);
        var member = (await Repo.GetByIdAsync(id))!;

        var (ok, _, _, tx) = MemberCalculator.AdjustBalance(member, 80m, "上月多扣", "店长", 1);
        Assert.True(ok);
        await Repo.AddTransactionAsync(tx!);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal(80m, reloaded.Balance);

        var txs = await Repo.GetTransactionsAsync(id);
        Assert.Equal("上月多扣", txs[0].Reason);
        Assert.Equal(-20m, txs[0].Amount);
        Assert.Equal(MemberTransactionType.BalanceAdjust, txs[0].Type);
    }

    [Fact]
    public async Task 消费_余额减少()
    {
        var id = await NewMemberAsync(balance: 100m);
        var member = (await Repo.GetByIdAsync(id))!;

        var (ok, _, _, tx) = MemberCalculator.Consume(member, 30m, "L20261014-0001", "店长", 1);
        Assert.True(ok);
        await Repo.AddTransactionAsync(tx!);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal(70m, reloaded.Balance);

        var txs = await Repo.GetTransactionsAsync(id);
        Assert.Equal("L20261014-0001", txs[0].OrderNo);
    }

    [Fact]
    public async Task 积分调整_独立于余额()
    {
        var id = await NewMemberAsync(balance: 100m, points: 50);
        var member = (await Repo.GetByIdAsync(id))!;

        var (ok, _, _, tx) = MemberCalculator.AdjustPoints(member, 80, "活动补发", "店长", 1);
        Assert.True(ok);
        await Repo.AddTransactionAsync(tx!);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal(80, reloaded.Points);
        Assert.Equal(100m, reloaded.Balance);   // 钱没动
    }

    [Fact]
    public async Task 注销_余额退掉且状态变更()
    {
        var id = await NewMemberAsync(balance: 88.5m);
        var member = (await Repo.GetByIdAsync(id))!;

        var (ok, _, _, tx) = MemberCalculator.CancelRefund(member, "现金", "店长", 1);
        Assert.True(ok);
        await Repo.AddTransactionAsync(tx!);
        await Repo.SetDataStatusAsync(id, 99);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal(0m, reloaded.Balance);
        Assert.False(reloaded.IsActive);

        // 流水要保留，注销不等于删记录
        var txs = await Repo.GetTransactionsAsync(id);
        Assert.Single(txs);
        Assert.Equal(MemberTransactionType.CancelRefund, txs[0].Type);
    }

    [Fact]
    public async Task 搜索_按卡号手机姓名()
    {
        await NewMemberAsync("M000001");
        await Repo.SaveAsync(new Member { CardNo = "M000002", Name = "李四", Phone = "13900139000" });

        Assert.Single(await Repo.SearchAsync("M000001"));
        Assert.Single(await Repo.SearchAsync("13900139000"));
        Assert.Single(await Repo.SearchAsync("李四"));
        Assert.Equal(2, (await Repo.SearchAsync("")).Count);
    }

    [Fact]
    public async Task 搜索_注销的排后面()
    {
        var dead = await NewMemberAsync("M000001");
        await NewMemberAsync("M000002");
        await Repo.SetDataStatusAsync(dead, 99);

        var list = await Repo.SearchAsync("");

        Assert.Equal("M000002", list[0].CardNo);
        Assert.Equal("M000001", list[1].CardNo);
    }

    [Fact]
    public async Task 编辑资料_不动余额()
    {
        var id = await NewMemberAsync(balance: 100m);
        var member = (await Repo.GetByIdAsync(id))!;

        member.Name = "张三丰";
        member.Level = "金卡";
        await Repo.SaveAsync(member);

        var reloaded = (await Repo.GetByIdAsync(id))!;
        Assert.Equal("张三丰", reloaded.Name);
        Assert.Equal("金卡", reloaded.Level);
        Assert.Equal(100m, reloaded.Balance);   // 改资料不该动钱
    }
}