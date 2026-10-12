using Xunit;
using YiHua.Pos.Hardware.Devices;

namespace YiHua.Pos.Tests.Hardware.Devices;

public class SecondaryScreenManagerTests
{
    [Fact]
    public void IsAvailable_False_With_SingleScreen()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 1);
        Assert.False(mgr.IsAvailable);
    }

    [Fact]
    public void IsAvailable_True_With_DualScreen()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        Assert.True(mgr.IsAvailable);
    }

    [Fact]
    public async Task ShowIdleAsync_Sets_IdleState()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        await mgr.ShowIdleAsync();

        Assert.Equal(SecondaryScreenState.Idle, mgr.Current.State);
        Assert.Equal("欢迎光临", mgr.Current.Title);
    }

    [Fact]
    public async Task ShowOrderAsync_Adapts_ItemList()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        var items = new List<SecondaryScreenItem>
        {
            new() { Name = "宫保鸡丁", Qty = 2, Price = 32m, Amount = 64m },
        };

        await mgr.ShowOrderAsync(items);

        Assert.Equal(SecondaryScreenState.Ordering, mgr.Current.State);
        Assert.Single(mgr.Current.Items);
        Assert.Equal(64m, mgr.Current.AmountTotal);
    }

    [Fact]
    public async Task ShowAdAsync_Sets_ImagePath()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        await mgr.ShowAdAsync("/ads/promo.png");

        Assert.Equal(SecondaryScreenState.Idle, mgr.Current.State);
        Assert.Equal("/ads/promo.png", mgr.Current.ImagePath);
    }

    [Fact]
    public async Task ShowSettlingAsync_Sets_Amounts()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        await mgr.ShowSettlingAsync(128m, 140m, 12m);

        Assert.Equal(SecondaryScreenState.Settling, mgr.Current.State);
        Assert.Equal(128m, mgr.Current.AmountTotal);
        Assert.Equal(140m, mgr.Current.AmountReceived);
        Assert.Equal(12m, mgr.Current.AmountChange);
    }

    [Fact]
    public async Task DataChanged_Raised_On_Publish()
    {
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2);
        SecondaryScreenData? received = null;
        mgr.DataChanged += (_, d) => received = d;

        await mgr.ShowIdleAsync();

        Assert.NotNull(received);
        Assert.Equal(SecondaryScreenState.Idle, received!.State);
    }

    private sealed class FakeView : ISecondaryScreenView
    {
        public SecondaryScreenData? LastShown { get; private set; }
        public int HideCalls { get; private set; }
        public void Show(SecondaryScreenData data) => LastShown = data;
        public void Hide() => HideCalls++;
    }

    [Fact]
    public async Task View_Shown_When_ScreenAvailable()
    {
        var view = new FakeView();
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 2, view: view);

        await mgr.ShowIdleAsync();

        Assert.NotNull(view.LastShown);
        Assert.Equal(0, view.HideCalls);
    }

    [Fact]
    public async Task View_Hidden_When_No_SecondScreen()
    {
        var view = new FakeView();
        var mgr = new SecondaryScreenManager(screenCountProvider: () => 1, view: view);

        await mgr.ShowIdleAsync();

        Assert.Null(view.LastShown);
        Assert.Equal(1, view.HideCalls);
    }
}
