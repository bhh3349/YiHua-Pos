using Xunit;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.Tests.UI.Ordering;

public class SubmitStateMachineTests
{
    [Fact]
    public async Task Rapid_Clicks_10_Times_Only_One_Submit()
    {
        var sm = new SubmitStateMachine();
        int calls = 0;

        async Task<(bool, long, bool, string?)> SubmitFn(string no, CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50, ct); // 模拟网络延迟
            return (true, 1, false, null);
        }

        // 连点 10 次
        var tasks = Enumerable.Range(0, 10).Select(_ => sm.SubmitAsync(SubmitFn));
        var results = await Task.WhenAll(tasks);

        int successCount = results.Count(r => r.IsSuccess);
        int ignoredCount = results.Count(r => r.IsDuplicateIgnored);

        Assert.Equal(1, successCount);
        Assert.Equal(9, ignoredCount);
        Assert.Equal(1, calls); // 实际只提交了一次
    }

    [Fact]
    public async Task Retry_Reuses_Same_ClientOrderNo()
    {
        var sm = new SubmitStateMachine();
        var usedNos = new List<string>();
        int attempt = 0;

        async Task<(bool, long, bool, string?)> SubmitFn(string no, CancellationToken ct)
        {
            usedNos.Add(no);
            await Task.Delay(10, ct);
            attempt++;
            if (attempt == 1) return (false, 0, false, "网络超时"); // 第一次失败
            return (true, 42, false, null);                        // 重试成功
        }

        var r1 = await sm.SubmitAsync(SubmitFn);
        Assert.False(r1.IsSuccess);
        Assert.Equal(SubmitState.Failed, sm.State);

        var r2 = await sm.SubmitAsync(SubmitFn);
        Assert.True(r2.IsSuccess);

        // 两次用同一个幂等键
        Assert.Equal(2, usedNos.Count);
        Assert.Equal(usedNos[0], usedNos[1]);
    }

    [Fact]
    public async Task Conflict_State_Set_On_Conflict()
    {
        var sm = new SubmitStateMachine();

        async Task<(bool, long, bool, string?)> SubmitFn(string no, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            return (false, 0, true, "订单已被结账");
        }

        var r = await sm.SubmitAsync(SubmitFn);

        Assert.True(r.IsConflict);
        Assert.Equal(SubmitState.Conflict, sm.State);
        Assert.Equal("订单已被结账", sm.ErrorMessage);
    }

    [Fact]
    public async Task Success_Resets_For_Next_Order()
    {
        var sm = new SubmitStateMachine();
        async Task<(bool, long, bool, string?)> Ok(string no, CancellationToken ct)
            => (true, 1, false, null);

        var r1 = await sm.SubmitAsync(Ok);
        string no1 = r1.ClientOrderNo!;

        sm.Reset();
        var r2 = await sm.SubmitAsync(Ok);
        string no2 = r2.ClientOrderNo!;

        Assert.NotEqual(no1, no2); // 新订单用新幂等键
        Assert.Equal(SubmitState.Succeeded, sm.State);
    }

    [Fact]
    public async Task StateChanged_Event_Fires()
    {
        var sm = new SubmitStateMachine();
        var states = new List<SubmitState>();
        sm.StateChanged += (_, s) => states.Add(s);

        async Task<(bool, long, bool, string?)> Ok(string no, CancellationToken ct)
            => (true, 1, false, null);

        await sm.SubmitAsync(Ok);

        Assert.Contains(SubmitState.Submitting, states);
        Assert.Contains(SubmitState.Succeeded, states);
    }
}
