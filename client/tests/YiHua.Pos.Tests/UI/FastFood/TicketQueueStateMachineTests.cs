using Xunit;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.Tests.UI.FastFood;

public class TicketQueueStateMachineTests
{
    private static TicketQueueStateMachine Create() =>
        new(new InMemoryTicketQueue());

    [Fact]
    public void Issue_Increments_Daily_Sequence()
    {
        var sm = Create();

        var t1 = sm.Issue();
        var t2 = sm.Issue();
        var t3 = sm.Issue(3);

        Assert.Equal("A001", t1.Number);
        Assert.Equal("A002", t2.Number);
        Assert.Equal("A003", t3.Number);
        Assert.Equal(3, t3.GuestCount);
        Assert.Equal(3, sm.IssuedToday);
    }

    [Fact]
    public void CallNext_Takes_First_Waiting()
    {
        var sm = Create();
        sm.Issue(); // A001
        sm.Issue(); // A002

        var called = sm.CallNext();

        Assert.NotNull(called);
        Assert.Equal("A001", called.Number);
        Assert.Equal("A001", sm.CurrentNumber);
        Assert.Equal("正在点单", sm.CurrentStatusText);
        Assert.Single(sm.WaitingNumbers);
        Assert.Equal("A002", sm.WaitingNumbers[0]);
    }

    [Fact]
    public void CallNext_Empty_Returns_Null()
    {
        var sm = Create();
        Assert.Null(sm.CallNext());
        Assert.Equal("暂未叫号", sm.CurrentStatusText);
    }

    [Fact]
    public void CallSpecific_Jumps_Queue()
    {
        var sm = Create();
        sm.Issue(); // A001
        sm.Issue(); // A002
        sm.Issue(); // A003

        var called = sm.CallSpecific("A003");

        Assert.NotNull(called);
        Assert.Equal("A003", called.Number);
        Assert.Equal(2, sm.WaitingCount); // A001, A002 仍在等待
    }

    [Fact]
    public void CallSpecific_NonWaiting_Returns_Null()
    {
        var sm = Create();
        sm.Issue();
        Assert.Null(sm.CallSpecific("A999")); // 不存在的号
        Assert.NotNull(sm.CallSpecific("A001")); // 等待中，可以指定叫
        Assert.Null(sm.CallSpecific("A001")); // 已叫号，不能再叫
    }

    [Fact]
    public void Skip_Moves_To_End_Of_Queue()
    {
        var sm = Create();
        sm.Issue(); // A001
        sm.Issue(); // A002
        sm.CallNext(); // 叫 A001

        Assert.True(sm.Skip("A001"));

        // A001 回到队尾等待
        Assert.Equal(2, sm.WaitingCount);
        Assert.Equal("A002", sm.WaitingNumbers[0]);
        Assert.Equal("A001", sm.WaitingNumbers[1]);
    }

    [Fact]
    public void Prioritize_Moves_To_Front()
    {
        var sm = Create();
        sm.Issue(); // A001
        sm.Issue(); // A002
        sm.Issue(); // A003

        Assert.True(sm.Prioritize("A003")); // 插队

        Assert.Equal("A003", sm.WaitingNumbers[0]);
        var called = sm.CallNext();
        Assert.Equal("A003", called!.Number);
    }

    [Fact]
    public void Prioritize_NonWaiting_Returns_False()
    {
        var sm = Create();
        sm.Issue();
        Assert.False(sm.Prioritize("A999"));
        sm.CallNext();
        Assert.False(sm.Prioritize("A001")); // 已叫号不能插队
    }

    [Fact]
    public void Void_Removes_From_Queue()
    {
        var sm = Create();
        sm.Issue(); // A001
        sm.Issue(); // A002

        Assert.True(sm.Void("A001"));

        Assert.Single(sm.WaitingNumbers);
        Assert.Equal("A002", sm.WaitingNumbers[0]);
    }

    [Fact]
    public void CompleteCurrent_Clears_Current()
    {
        var sm = Create();
        sm.Issue();
        sm.CallNext();

        Assert.True(sm.CompleteCurrent());
        Assert.Equal(string.Empty, sm.CurrentNumber);
        Assert.False(sm.CompleteCurrent()); // 无当前号
    }

    [Fact]
    public void TicketCalled_Event_Fires()
    {
        var sm = Create();
        sm.Issue();
        Ticket? fired = null;
        sm.TicketCalled += (_, t) => fired = t;

        sm.CallNext();

        Assert.NotNull(fired);
        Assert.Equal("A001", fired.Number);
    }
}
