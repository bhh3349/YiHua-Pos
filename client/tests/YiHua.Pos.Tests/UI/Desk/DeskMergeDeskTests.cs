using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Desk;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;
using Xunit;

namespace YiHua.Pos.Tests.UI.Desk;


/// <summary>并台测试：明细合并、人数相加、一桌变空闲</summary>
public class DeskMergeDeskTests
{
    [Fact]
    public async Task Merge_Combines_Guests_And_Amounts()
    {
        var repo = new FakeDeskRepository();
        var a = new DeskEntity { Id = 1, Name = "A01", Status = DeskStatus.Occupied, GuestCount = 2, CurrentAmount = 50m };
        var b = new DeskEntity { Id = 2, Name = "A02", Status = DeskStatus.Occupied, GuestCount = 3, CurrentAmount = 80m };
        var vm = new DeskMergeViewModel(repo, a) { TargetDesk = b };

        bool ok = await vm.ConfirmAsync();

        Assert.True(ok);
        Assert.Equal(5, b.GuestCount);          // 人数相加
        Assert.Equal(130m, b.CurrentAmount);    // 金额相加
        Assert.Equal(DeskStatus.Idle, a.Status); // 源桌变空闲
    }

    [Fact]
    public async Task Merge_Rejects_Idle_Desk()
    {
        var repo = new FakeDeskRepository();
        var a = new DeskEntity { Id = 1, Name = "A01", Status = DeskStatus.Idle };
        var b = new DeskEntity { Id = 2, Name = "A02", Status = DeskStatus.Occupied };
        var vm = new DeskMergeViewModel(repo, a) { TargetDesk = b };

        bool ok = await vm.ConfirmAsync();

        Assert.False(ok);
    }
}

