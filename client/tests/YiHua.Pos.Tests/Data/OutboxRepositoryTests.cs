using Xunit;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Tests.Data;

public class OutboxRepositoryTests : TempDbFixture
{
    private readonly OutboxRepository _repo;

    public OutboxRepositoryTests()
    {
        _repo = new OutboxRepository(Ctx.Db);
    }

    private static OutboxItem NewItem(string no, DateTime createdAt) => new()
    {
        ClientOrderNo = no,
        ApiPath = "/api/v1/order/settle",
        PayloadJson = $"{{\"no\":\"{no}\"}}",
        CreatedAt = createdAt,
    };

    [Fact]
    public async Task GetPending_ReturnsInCreatedAtOrder()
    {
        var t = new DateTime(2026, 10, 12, 12, 0, 0);
        // 故意乱序入队，取出时必须按 CreatedAt 升序
        await _repo.EnqueueAsync(NewItem("n3", t.AddMinutes(3)));
        await _repo.EnqueueAsync(NewItem("n1", t.AddMinutes(1)));
        await _repo.EnqueueAsync(NewItem("n2", t.AddMinutes(2)));

        var pending = await _repo.GetPendingAsync();
        Assert.Equal(3, pending.Count);
        Assert.Equal("n1", pending[0].ClientOrderNo);
        Assert.Equal("n2", pending[1].ClientOrderNo);
        Assert.Equal("n3", pending[2].ClientOrderNo);
    }

    [Fact]
    public async Task MarkSucceeded_RemovesFromPending()
    {
        var id = await _repo.EnqueueAsync(NewItem("ok1", DateTime.Now));
        Assert.Equal(1, await _repo.CountPendingAsync());

        await _repo.MarkSucceededAsync(id);

        Assert.Equal(0, await _repo.CountPendingAsync());
        var item = await Ctx.Db.Queryable<OutboxItem>().Where(o => o.Id == id).FirstAsync();
        Assert.Equal(SyncStatus.Succeeded, item!.Status);
        Assert.NotNull(item.SentAt);
    }

    [Fact]
    public async Task MarkFailed_MarksFailedAfterMaxRetries()
    {
        var id = await _repo.EnqueueAsync(NewItem("bad1", DateTime.Now));

        for (var i = 0; i < OutboxRepository.MaxRetries; i++)
            await _repo.MarkFailedAsync(id, $"err{i}");

        var item = await Ctx.Db.Queryable<OutboxItem>().Where(o => o.Id == id).FirstAsync();
        Assert.Equal(OutboxRepository.MaxRetries, item!.RetryCount);
        Assert.Equal(SyncStatus.Failed, item.Status);
        Assert.Equal($"err{OutboxRepository.MaxRetries - 1}", item.LastError);

        // Failed 不再出现在待补传队列
        Assert.Empty(await _repo.GetPendingAsync());
    }

    [Fact]
    public async Task MarkFailed_BelowMaxRetries_StaysPending()
    {
        var id = await _repo.EnqueueAsync(NewItem("retry1", DateTime.Now));
        await _repo.MarkFailedAsync(id, "timeout");

        var item = await Ctx.Db.Queryable<OutboxItem>().Where(o => o.Id == id).FirstAsync();
        Assert.Equal(1, item!.RetryCount);
        Assert.Equal(SyncStatus.Pending, item.Status);
        Assert.Single(await _repo.GetPendingAsync());
    }
}
