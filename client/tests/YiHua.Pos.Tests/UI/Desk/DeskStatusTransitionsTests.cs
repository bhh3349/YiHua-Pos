using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Desk;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;
using Xunit;

namespace YiHua.Pos.Tests.UI.Desk;

/// <summary>桌台状态机测试：合法转换通过，5 种非法转换全部被拒绝</summary>
public class DeskStatusTransitionsTests
{
    // id 必须随桌号变化：并台/换台会校验「不能是同一张桌」，
    // 写死 Id=1 会让所有双桌用例都撞上「自己和自己」而被拒。
    private static DeskEntity MakeDesk(DeskStatus status, string name = "A01") =>
        new() { Id = Math.Abs(name.GetHashCode()) + 1, Name = name, Status = status };

    [Fact]
    public void Open_Idle_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryOpen(MakeDesk(DeskStatus.Idle)).Ok);

    [Fact]
    public void Open_Reserved_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryOpen(MakeDesk(DeskStatus.Reserved)).Ok);

    [Fact]
    public void Open_Occupied_Rejected() =>
        Assert.False(DeskStatusTransitions.TryOpen(MakeDesk(DeskStatus.Occupied)).Ok);

    [Fact]
    public void Change_IdleSource_Rejected()
    {
        var result = DeskStatusTransitions.TryChange(
            MakeDesk(DeskStatus.Idle, "A01"), MakeDesk(DeskStatus.Idle, "A02"), false);
        Assert.False(result.Ok);
        Assert.Contains("A01", result.Error);
    }

    [Fact]
    public void Change_TargetNotIdle_Rejected()
    {
        var result = DeskStatusTransitions.TryChange(
            MakeDesk(DeskStatus.Occupied, "A01"), MakeDesk(DeskStatus.Occupied, "A02"), false);
        Assert.False(result.Ok);
        Assert.Contains("A02", result.Error);
    }

    [Fact]
    public void Change_UnsettledSource_Rejected()
    {
        var result = DeskStatusTransitions.TryChange(
            MakeDesk(DeskStatus.Occupied, "A01"), MakeDesk(DeskStatus.Idle, "A02"), sourceHasUnsettledItems: true);
        Assert.False(result.Ok);
        Assert.Contains("清台", result.Error);
    }

    [Fact]
    public void Merge_IdleDesk_Rejected()
    {
        var result = DeskStatusTransitions.TryMerge(
            MakeDesk(DeskStatus.Idle, "A01"), MakeDesk(DeskStatus.Occupied, "A02"));
        Assert.False(result.Ok);
    }

    [Fact]
    public void Merge_BothOccupied_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryMerge(
            MakeDesk(DeskStatus.Occupied, "A01"), MakeDesk(DeskStatus.Occupied, "A02")).Ok);

    [Fact]
    public void Clear_Unsettled_Rejected()
    {
        var result = DeskStatusTransitions.TryClear(MakeDesk(DeskStatus.Occupied), isSettled: false);
        Assert.False(result.Ok);
        Assert.Contains("结账", result.Error);
    }

    [Fact]
    public void Clear_Settled_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryClear(MakeDesk(DeskStatus.Occupied), isSettled: true).Ok);

    [Fact]
    public void Clear_Reserved_Rejected()
    {
        var result = DeskStatusTransitions.TryClear(MakeDesk(DeskStatus.Reserved), isSettled: true);
        Assert.False(result.Ok);
        Assert.Contains("预订", result.Error);
    }

    [Fact]
    public void Reserve_Idle_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryReserve(MakeDesk(DeskStatus.Idle)).Ok);

    [Fact]
    public void Reserve_Occupied_Rejected() =>
        Assert.False(DeskStatusTransitions.TryReserve(MakeDesk(DeskStatus.Occupied)).Ok);

    [Fact]
    public void CancelReserve_Reserved_Succeeds() =>
        Assert.True(DeskStatusTransitions.TryCancelReserve(MakeDesk(DeskStatus.Reserved)).Ok);
}
