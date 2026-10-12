using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Desk;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;
using Xunit;

namespace YiHua.Pos.Tests.UI.Desk;


/// <summary>清台测试：未结账拒绝，已结账成功</summary>
public class DeskClearDeskTests
{
    [Fact]
    public async Task Clear_Unsettled_Rejected()
    {
        var repo = new FakeDeskRepository();
        var desk = new DeskEntity { Id = 1, Name = "A01", Status = DeskStatus.Occupied };
        var vm = new DeskClearViewModel(repo, desk, "张三");

        bool ok = await vm.ConfirmAsync(isSettled: false);

        Assert.False(ok);
        Assert.Contains("结账", vm.ErrorMessage);
        Assert.Equal(DeskStatus.Occupied, desk.Status); // 状态不变
    }

    [Fact]
    public async Task Clear_Settled_Succeeds()
    {
        var repo = new FakeDeskRepository();
        var desk = new DeskEntity
        {
            Id = 1, Name = "A01", Status = DeskStatus.Occupied,
            GuestCount = 4, CurrentOrderId = 100, OpenTime = DateTime.Now,
        };
        var vm = new DeskClearViewModel(repo, desk, "张三");
        bool cleared = false;
        vm.Cleared += (s, e) => cleared = true;

        bool ok = await vm.ConfirmAsync(isSettled: true);

        Assert.True(ok);
        Assert.True(cleared);
        Assert.Equal(DeskStatus.Idle, desk.Status);
        Assert.Equal(0, desk.GuestCount);
    }
}
