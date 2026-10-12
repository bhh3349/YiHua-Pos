using Xunit;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.Tests.UI.FastFood;

/// <summary>测试用假叫号屏</summary>
public sealed class FakeCounterDisplay : ICounterDisplay
{
    public bool Available { get; set; } = true;
    public CallBoardData? LastShown { get; private set; }
    public int ShowCount { get; private set; }

    public bool IsAvailable => Available;

    public void Show(CallBoardData data)
    {
        LastShown = data;
        ShowCount++;
    }
}

public class CallBoardTests
{
    [Fact]
    public void Update_Pushes_To_Display_When_Available()
    {
        var display = new FakeCounterDisplay();
        var vm = new CallBoardViewModel(display);

        vm.Update("A052", "正在点单", new[] { "A053", "A054" }, 2, 58m);

        Assert.Equal("A052", vm.CurrentNumber);
        Assert.Equal("正在点单", vm.CurrentStatus);
        Assert.False(vm.IsDowngraded);
        Assert.NotNull(display.LastShown);
        Assert.Equal("A052", display.LastShown.CurrentNumber);
        Assert.Equal(2, display.LastShown.WaitingNumbers.Count);
    }

    [Fact]
    public void Downgrade_When_Display_Unavailable()
    {
        var display = new FakeCounterDisplay { Available = false };
        var vm = new CallBoardViewModel(display);

        vm.Update("A052", "正在点单", new[] { "A053" }, 1);

        // 降级：不推送，UI 用 ViewModel 属性直接渲染
        Assert.True(vm.IsDowngraded);
        Assert.Null(display.LastShown);
        Assert.NotEmpty(vm.DowngradeHint);
        Assert.Contains("副屏", vm.DowngradeHint);
        // 数据仍在 ViewModel 上，主屏可渲染
        Assert.Equal("A052", vm.CurrentNumber);
        Assert.Single(vm.WaitingNumbers);
    }

    [Fact]
    public void Downgrade_Recovers_When_Display_Back()
    {
        var display = new FakeCounterDisplay { Available = false };
        var vm = new CallBoardViewModel(display);
        vm.Update("A052", "正在点单", Array.Empty<string>(), 0);
        Assert.True(vm.IsDowngraded);

        display.Available = true;
        vm.Update("A053", "正在点单", Array.Empty<string>(), 0);

        Assert.False(vm.IsDowngraded);
        Assert.NotNull(display.LastShown);
    }

    [Fact]
    public void ShowAmount_Toggles_Push()
    {
        var display = new FakeCounterDisplay();
        var vm = new CallBoardViewModel(display);
        vm.Update("A052", "正在点单", Array.Empty<string>(), 0, 58m);

        int before = display.ShowCount;
        vm.ShowAmount = true;

        Assert.True(display.ShowCount > before);
        Assert.True(display.LastShown!.ShowAmount);
        Assert.Equal(58m, display.LastShown.CurrentAmount);
    }

    [Fact]
    public void WaitingNumbers_Limited_To_8()
    {
        var display = new FakeCounterDisplay();
        var vm = new CallBoardViewModel(display);
        var many = Enumerable.Range(1, 20).Select(i => $"A{i:D3}");

        vm.Update("A001", "正在点单", many, 20);

        Assert.Equal(8, vm.WaitingNumbers.Count);
    }
}
