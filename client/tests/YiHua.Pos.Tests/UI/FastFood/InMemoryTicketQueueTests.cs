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
}
