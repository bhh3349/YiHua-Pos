using Xunit;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.HangOrder;

namespace YiHua.Pos.Tests.UI.HangOrder;

public class HangOrderTimeoutTests
{
    [Fact]
    public void IsTimedOut_Exactly_At_Threshold()
    {
        var record = new HangOrderRecord { ParkTime = DateTime.Now.AddHours(-4) };
        // 边界：刚好 4 小时算超时
        Assert.True(HangOrderCalculator.IsTimedOut(record, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void IsTimedOut_Just_Before_Threshold()
    {
        var record = new HangOrderRecord { ParkTime = DateTime.Now.AddHours(-4).AddSeconds(10) };
        Assert.False(HangOrderCalculator.IsTimedOut(record, TimeSpan.FromHours(4)));
    }

    [Fact]
    public void IsTimedOut_Custom_Threshold()
    {
        var record = new HangOrderRecord { ParkTime = DateTime.Now.AddHours(-2) };
        Assert.True(HangOrderCalculator.IsTimedOut(record, TimeSpan.FromHours(1)));
        Assert.False(HangOrderCalculator.IsTimedOut(record, TimeSpan.FromHours(3)));
    }

    [Fact]
    public async Task CleanViewModel_Loads_Only_TimedOut()
    {
        var repo = new InMemoryHangOrderRepository();
        await repo.ParkAsync(new HangOrderRecord
        {
            DeskName = "A08", Amount = 100m,
            ParkTime = DateTime.Now.AddHours(-5), // 超时
        });
        await repo.ParkAsync(new HangOrderRecord
        {
            DeskName = "A09", Amount = 50m,
            ParkTime = DateTime.Now.AddHours(-1), // 未超时
        });

        var vm = new HangOrderCleanViewModel(repo);
        await vm.LoadTimedOutAsync(TimeSpan.FromHours(4));

        Assert.Equal(1, vm.CleanCount);
        Assert.Equal(100m, vm.CleanAmount);
        Assert.Contains("1", vm.ConfirmText);
        Assert.Contains("100", vm.ConfirmText);
    }

    [Fact]
    public async Task CleanViewModel_Clean_Removes_Only_TimedOut()
    {
        var repo = new InMemoryHangOrderRepository();
        await repo.ParkAsync(new HangOrderRecord
        {
            DeskName = "A08", Amount = 100m,
            ParkTime = DateTime.Now.AddHours(-5),
        });
        await repo.ParkAsync(new HangOrderRecord
        {
            DeskName = "A09", Amount = 50m,
            ParkTime = DateTime.Now.AddHours(-1),
        });

        var vm = new HangOrderCleanViewModel(repo);
        await vm.LoadTimedOutAsync(TimeSpan.FromHours(4));
        int cleaned = await vm.CleanAsync();

        Assert.Equal(1, cleaned);
        Assert.Equal(1, await repo.CountAsync()); // 未超时的保留
    }

    [Fact]
    public async Task ViewModel_TimedOutCount()
    {
        var repo = new InMemoryHangOrderRepository();
        await repo.ParkAsync(new HangOrderRecord { DeskName = "A08", ParkTime = DateTime.Now.AddHours(-5) });
        await repo.ParkAsync(new HangOrderRecord { DeskName = "A09", ParkTime = DateTime.Now.AddHours(-1) });

        var vm = new HangOrderViewModel(repo);
        await vm.LoadAsync();

        Assert.Equal(1, vm.TimedOutCount);
        Assert.False(vm.IsTimedOut(vm.Records[0])); // 倒序：第一个是1小时前的（未超时）
        Assert.True(vm.IsTimedOut(vm.Records[1]));  // 第二个是5小时前的（超时）
    }
}
