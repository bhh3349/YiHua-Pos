using Xunit;
using YiHua.Pos.UI.Services.SoldOut;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

public class SoldOutSyncTests
{
    [Fact]
    public async Task SelfPlatform_Always_Succeeds()
    {
        var sync = new SelfPlatformSoldOutSync();
        Assert.True(sync.IsConnected);
        Assert.Equal("self_platform", sync.PlatformId);

        var r1 = await sync.PushSoldOutAsync(1);
        var r2 = await sync.PushRestoreAsync(1);
        Assert.True(r1.Success);
        Assert.True(r2.Success);
    }

    [Fact]
    public async Task Skeleton_Returns_NotSupported_No_Crash()
    {
        var sync = new SkeletonSoldOutSync("meituan");
        Assert.False(sync.IsConnected);

        var r1 = await sync.PushSoldOutAsync(1);
        var r2 = await sync.PushRestoreAsync(1);

        Assert.True(r1.NotSupported);
        Assert.True(r2.NotSupported);
        Assert.False(r1.Success);
        Assert.NotNull(r1.Error);
    }

    [Fact]
    public async Task Service_PushToPlatforms_Skips_Unsupported_Silently()
    {
        var repo = new FakeSoldOutRepository();
        var logs = new List<string>();
        var service = new SoldOutService(
            repo, new SystemClock(),
            new ISoldOutSync[]
            {
                new SelfPlatformSoldOutSync(),
                new SkeletonSoldOutSync("meituan"),
                new SkeletonSoldOutSync("eleme"),
            },
            logs.Add);

        // 不抛异常，未接入的不记为错误日志
        await service.PushToPlatformsAsync(1, soldOut: true);

        Assert.Empty(logs); // NotSupported 不记日志
    }

    [Fact]
    public async Task Service_Sync_Failure_Does_Not_Block_Local()
    {
        var repo = new FakeSoldOutRepository();
        var logs = new List<string>();
        var failing = new FailingSync();
        var service = new SoldOutService(
            repo, new SystemClock(),
            new ISoldOutSync[] { failing },
            logs.Add);

        // 同步失败不抛异常
        await service.PushToPlatformsAsync(1, soldOut: true);

        Assert.Single(logs); // 记了日志
        Assert.Contains("fail-platform", logs[0]);
    }

    private sealed class FailingSync : ISoldOutSync
    {
        public string PlatformId => "fail-platform";
        public bool IsConnected => true;
        public Task<SoldOutSyncResult> PushSoldOutAsync(long productId, CancellationToken ct = default)
            => throw new InvalidOperationException("网络故障");
        public Task<SoldOutSyncResult> PushRestoreAsync(long productId, CancellationToken ct = default)
            => throw new InvalidOperationException("网络故障");
    }
}
