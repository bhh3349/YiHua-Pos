using Xunit;
using YiHua.Pos.UI.Services.SoldOut;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

public class SoldOutSchedulerTests
{
    [Fact]
    public async Task RunOnce_Restores_Manual_After_Reset()
    {
        var repo = new FakeSoldOutRepository();
        var clock = new FakeClock(new DateTime(2026, 10, 12, 10, 0, 0)); // 10:00，已过 09:00
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 1, ProductName = "牛肉面",
            Source = SoldOutSource.Manual,
            SoldOutTime = new DateTime(2026, 10, 11, 20, 0, 0),
        });

        var scheduler = new SoldOutScheduler(repo, clock);
        var restored = await scheduler.RunOnceAsync();

        Assert.Single(restored);
        Assert.Equal(1L, restored[0]);
        Assert.Equal(0, await repo.CountAsync());
    }

    [Fact]
    public async Task RunOnce_Not_Yet_Time_No_Restore()
    {
        var repo = new FakeSoldOutRepository();
        var clock = new FakeClock(new DateTime(2026, 10, 12, 8, 0, 0)); // 08:00，未到 09:00
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 1, ProductName = "牛肉面",
            Source = SoldOutSource.Manual,
            SoldOutTime = new DateTime(2026, 10, 11, 20, 0, 0),
        });

        var scheduler = new SoldOutScheduler(repo, clock);
        var restored = await scheduler.RunOnceAsync();

        Assert.Empty(restored);
        Assert.Equal(1, await repo.CountAsync()); // 还在
    }

    [Fact]
    public async Task RunOnce_Custom_Reset_Time()
    {
        var repo = new FakeSoldOutRepository();
        var clock = new FakeClock(new DateTime(2026, 10, 12, 7, 0, 0));
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 1, ProductName = "牛肉面",
            Source = SoldOutSource.Manual,
            SoldOutTime = new DateTime(2026, 10, 11, 20, 0, 0),
        });

        var scheduler = new SoldOutScheduler(repo, clock)
        {
            DailyResetTime = new TimeSpan(6, 0, 0), // 改成 06:00
        };
        var restored = await scheduler.RunOnceAsync();

        Assert.Single(restored); // 07:00 已过 06:00，恢复
    }

    [Fact]
    public void DailyResetText_Human_Readable()
    {
        var scheduler = new SoldOutScheduler(
            new FakeSoldOutRepository(), new FakeClock(DateTime.Now));
        Assert.Equal("每天 09:00 恢复供应", scheduler.DailyResetText);

        scheduler.DailyResetTime = new TimeSpan(14, 30, 0);
        Assert.Equal("每天 14:30 恢复供应", scheduler.DailyResetText);
    }

    [Fact]
    public async Task RunOnce_Scheduled_Restores_At_Window_End()
    {
        var repo = new FakeSoldOutRepository();
        var clock = new FakeClock(new DateTime(2026, 10, 12, 14, 0, 0));
        await repo.SaveAsync(new SoldOutRecord
        {
            ProductId = 2, ProductName = "午市套餐",
            Source = SoldOutSource.Scheduled,
            SoldOutTime = new DateTime(2026, 10, 12, 11, 0, 0),
            ScheduledRestoreTime = new DateTime(2026, 10, 12, 14, 0, 0),
        });

        var scheduler = new SoldOutScheduler(repo, clock);
        var restored = await scheduler.RunOnceAsync();

        Assert.Single(restored);
    }
}
