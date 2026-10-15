using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.FastFood;
using Xunit;

namespace YiHua.Pos.Tests.UI.FastFood;

/// <summary>
/// 叫号取餐测试。
///
/// 守的是队列状态机：发号 → 叫号 → 取餐/跳号/作废。
/// 号叫错了客人白等，号丢了客人拿不到饭 ——
/// 所以每个状态迁移都要能说清楚从哪到哪。
/// </summary>
public class CallNumberTests
{
    private static CallNumberViewModel Vm() => new(new InMemoryTicketQueue("A"));

    [Fact]
    public void 发号_按日序号递增()
    {
        var vm = Vm();

        var t1 = vm.Issue();
        var t2 = vm.Issue();
        var t3 = vm.Issue();

        Assert.Equal("A001", t1.Number);
        Assert.Equal("A002", t2.Number);
        Assert.Equal("A003", t3.Number);
        Assert.Equal(3, vm.IssuedToday);
    }

    [Fact]
    public void 发号_带人数()
    {
        var vm = Vm();

        var t = vm.Issue(4);

        Assert.Equal(4, t.GuestCount);
    }

    [Fact]
    public void 叫下一个_把等待中最前面的叫出来()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();

        vm.CallNext();

        Assert.Equal("A001", vm.CurrentNumber);
        Assert.Contains("已叫号", vm.CurrentStatusText);
    }

    [Fact]
    public void 没有等待的号_叫号要给明确提示()
    {
        var vm = Vm();

        vm.CallNext();

        Assert.Contains("没有等待", vm.ErrorMessage);
    }

    [Fact]
    public void 指定叫号_客人拿着号来问()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();
        vm.Issue();

        vm.CallSpecific("A003");

        Assert.Equal("A003", vm.CurrentNumber);
    }

    [Fact]
    public void 指定叫号_号不在队列里要拒绝()
    {
        var vm = Vm();
        vm.Issue();

        vm.CallSpecific("A999");

        Assert.Contains("不在等待队列", vm.ErrorMessage);
    }

    [Fact]
    public void 再次叫号_客人没听见是常态()
    {
        var vm = Vm();
        vm.Issue();
        vm.CallNext();

        vm.CallAgain();

        Assert.Equal("A001", vm.CurrentNumber);
        Assert.Contains("再次叫号", vm.InfoMessage);
    }

    [Fact]
    public void 再次叫号_还没叫过时要提示()
    {
        var vm = Vm();
        vm.Issue();

        vm.CallAgain();

        Assert.Contains("还没有叫过号", vm.ErrorMessage);
    }

    [Fact]
    public void 取餐_从队列移除()
    {
        var vm = Vm();
        vm.Issue();
        vm.CallNext();

        vm.Complete("A001");

        Assert.Contains("已取餐", vm.InfoMessage);
        Assert.DoesNotContain(vm.Waiting, t => t.Number == "A001");
    }

    [Fact]
    public void 跳号_叫了没人应()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();
        vm.CallNext();

        vm.Skip("A001");

        Assert.Contains("已跳号", vm.InfoMessage);
        // 跳号后下一个还是能叫到 A002
        vm.CallNext();
        Assert.Equal("A002", vm.CurrentNumber);
    }

    [Fact]
    public void 优先_把号提到最前()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();
        vm.Issue();

        vm.Prioritize("A003");
        vm.CallNext();

        Assert.Equal("A003", vm.CurrentNumber);
    }

    [Fact]
    public void 作废_客人走了()
    {
        var vm = Vm();
        vm.Issue();

        vm.Void("A001");

        Assert.Contains("已作废", vm.InfoMessage);
        Assert.Equal(0, vm.WaitingCount);
    }

    [Fact]
    public void 等待队列_显示等待时长()
    {
        var vm = Vm();
        vm.Issue();
        vm.Refresh();

        Assert.Single(vm.Waiting);
        Assert.Equal("刚刚", vm.Waiting[0].WaitedText);
    }

    [Fact]
    public void 制作中_等于已叫未取的数量()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();

        Assert.Equal(0, vm.MakingCount);

        vm.CallNext();
        vm.Refresh();

        Assert.Equal(1, vm.MakingCount);
    }

    [Fact]
    public void 等待数量_反映真实队列()
    {
        var vm = Vm();
        vm.Issue();
        vm.Issue();
        vm.Issue();
        vm.Refresh();

        Assert.Equal(3, vm.WaitingCount);

        // Complete 只对「已叫号」的号生效 —— 没叫过就取餐是走不通的
        vm.CallSpecific("A001");
        vm.Complete("A001");

        Assert.Equal(2, vm.WaitingCount);
    }

    [Fact]
    public void 取餐_必须先是已叫号状态()
    {
        var vm = Vm();
        vm.Issue();

        // 还没叫号就想取餐，应该被拒绝
        vm.Complete("A001");

        Assert.Contains("不在队列里", vm.ErrorMessage);
        Assert.Equal(1, vm.WaitingCount);
    }

    [Theory]
    [InlineData(0, 30, "刚刚")]
    [InlineData(0, 59, "刚刚")]
    [InlineData(0, 60, "1 分钟")]
    [InlineData(0, 59 * 60, "59 分钟")]
    public void 等待时长文案(int hours, int seconds, string expected)
    {
        var span = TimeSpan.FromHours(hours) + TimeSpan.FromSeconds(seconds);
        Assert.Equal(expected, CallNumberViewModel.FormatWaited(span));
    }

    [Fact]
    public void 等待时长_超过一小时显示小时和分钟()
    {
        Assert.Equal("1 小时", CallNumberViewModel.FormatWaited(TimeSpan.FromMinutes(60)));
        Assert.Equal("1 小时 30 分", CallNumberViewModel.FormatWaited(TimeSpan.FromMinutes(90)));
        Assert.Equal("2 小时 5 分", CallNumberViewModel.FormatWaited(TimeSpan.FromMinutes(125)));
    }

    [Fact]
    public void 当前叫号_没有号时显示占位()
    {
        var vm = Vm();
        vm.Refresh();

        Assert.Equal("—", vm.CurrentNumber);
        Assert.Equal("还没有叫号", vm.CurrentStatusText);
    }

    [Fact]
    public void 号前缀_按队列配置()
    {
        var vm = new CallNumberViewModel(new InMemoryTicketQueue("B"));

        var t = vm.Issue();

        Assert.StartsWith("B", t.Number);
    }
}