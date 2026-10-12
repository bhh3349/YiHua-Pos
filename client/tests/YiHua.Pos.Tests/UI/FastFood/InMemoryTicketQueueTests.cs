using Xunit;
using YiHua.Pos.UI.Services.FastFood;

namespace YiHua.Pos.Tests.UI.FastFood;

public class InMemoryTicketQueueTests
{
    [Fact]
    public void Concurrent_Issue_No_Duplicate_Numbers()
    {
        var queue = new InMemoryTicketQueue();
        var numbers = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 100, _ => numbers.Add(queue.Issue().Number));

        Assert.Equal(100, numbers.Distinct().Count());
        Assert.Equal(100, queue.IssuedToday);
    }

    [Fact]
    public void GetWaiting_Only_Returns_Waiting()
    {
        var queue = new InMemoryTicketQueue();
        queue.Issue(); // A001
        queue.Issue(); // A002
        queue.CallNext(); // A001 → Called

        var waiting = queue.GetWaiting();

        Assert.Single(waiting);
        Assert.Equal("A002", waiting[0].Number);
    }

    [Fact]
    public void GetCurrent_Returns_Called()
    {
        var queue = new InMemoryTicketQueue();
        queue.Issue();
        queue.Issue();
        queue.CallNext();

        var current = queue.GetCurrent();

        Assert.NotNull(current);
        Assert.Equal("A001", current.Number);
        Assert.Equal(TicketStatus.Called, current.Status);
    }

    [Fact]
    public void Void_Called_Ticket_Works()
    {
        var queue = new InMemoryTicketQueue();
        queue.Issue();
        queue.CallNext();

        Assert.True(queue.Void("A001"));
        Assert.Empty(queue.GetWaiting());
        Assert.Null(queue.GetCurrent());
    }

    [Fact]
    public void Complete_Requires_Called_Status()
    {
        var queue = new InMemoryTicketQueue();
        queue.Issue();

        Assert.False(queue.Complete("A001")); // 等待中不能完成

        queue.CallNext();
        Assert.True(queue.Complete("A001"));
    }

    [Fact]
    public void Custom_Prefix()
    {
        var queue = new InMemoryTicketQueue("B");
        Assert.Equal("B001", queue.Issue().Number);
    }

    [Fact]
    public void Issue_Sets_GuestCount_Min_1()
    {
        var queue = new InMemoryTicketQueue();
        Assert.Equal(1, queue.Issue(0).GuestCount);
        Assert.Equal(1, queue.Issue(-5).GuestCount);
    }

    /// <summary>跨零点后序号从 A001 重新开始。</summary>
    [Fact]
    public void Day_Rollover_Resets_Sequence()
    {
        var now = new DateTime(2026, 10, 12, 23, 58, 0);
        var queue = new InMemoryTicketQueue("A", () => now);

        Assert.Equal("A001", queue.Issue().Number);
        Assert.Equal("A002", queue.Issue().Number);

        now = now.AddDays(1); // 00:01 跨零点

        Assert.Equal("A001", queue.Issue().Number);
        Assert.Equal(1, queue.IssuedToday);
    }

    /// <summary>
    /// 快餐店通宵营业时的真实故障：昨天没叫到的号会一直挂在等待队列里，
    /// 而序号已回退，那个号永远等不到叫号。跨日必须把它们标记为过期。
    /// </summary>
    [Fact]
    public void Day_Rollover_Expires_Yesterdays_Waiting_Tickets()
    {
        var now = new DateTime(2026, 10, 12, 23, 58, 0);
        var queue = new InMemoryTicketQueue("A", () => now);

        var t1 = queue.Issue();      // A001 等待中
        var t2 = queue.Issue();      // A002 等待中
        queue.CallNext();            // A001 -> Called
        var called = queue.GetCurrent();

        now = now.AddDays(1);

        queue.Issue();     // 跨日触发重置，同时发出一张新票

        // 昨天留下的号全部转为过期，不再出现在等待队列里
        Assert.Equal(TicketStatus.Expired, t2.Status);   // 标记过期而非删除
        Assert.Equal(TicketStatus.Expired, called!.Status); // 已叫但没完成的也要过期
        Assert.DoesNotContain(queue.GetWaiting(), t => t.Status == TicketStatus.Expired);
    }

    /// <summary>
    /// 号码按日复用会造成同号两条记录（昨天的 A001 / 今天的 A001）。
    /// 过期号必须既不被叫号命中，也不干扰当天新号。
    /// </summary>
    [Fact]
    public void Expired_Ticket_Does_Not_Shadow_Same_Number_Today()
    {
        var now = new DateTime(2026, 10, 12, 23, 58, 0);
        var queue = new InMemoryTicketQueue("A", () => now);

        var yesterday = queue.Issue();      // 昨天的 A001
        now = now.AddDays(1);
        var today = queue.Issue();          // 今天的 A001（号码复用）

        Assert.Equal(yesterday.Number, today.Number);            // 确认号码确实复用
        Assert.Equal(TicketStatus.Expired, yesterday.Status);

        var called = queue.CallSpecific(today.Number);
        Assert.NotNull(called);
        Assert.Same(today, called);                               // 命中今天的记录
        Assert.Equal(TicketStatus.Expired, yesterday.Status);     // 昨天那条不受影响
    }
}