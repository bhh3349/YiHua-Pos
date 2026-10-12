using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Desk;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;
using Xunit;

namespace YiHua.Pos.Tests.UI.Desk;

/// <summary>筛选测试：区域 + 状态组合筛选，排序切换</summary>
public class DeskFilterTests
{
    private static DeskMapViewModel CreateVm()
    {
        var repo = new FakeDeskRepository();
        repo.Areas.AddRange(new[]
        {
            new DeskArea { Id = 1, Name = "大厅", Sort = 1 },
            new DeskArea { Id = 2, Name = "包间", Sort = 2 },
        });
        repo.Desks.AddRange(new[]
        {
            new DeskEntity { Id = 1, Name = "A01", AreaName = "大厅", Status = DeskStatus.Occupied },
            new DeskEntity { Id = 2, Name = "A02", AreaName = "大厅", Status = DeskStatus.Idle },
            new DeskEntity { Id = 3, Name = "B01", AreaName = "包间", Status = DeskStatus.Idle },
            new DeskEntity { Id = 4, Name = "B02", AreaName = "包间", Status = DeskStatus.Reserved },
        });
        return new DeskMapViewModel(repo);
    }

    [Fact]
    public async Task Filter_By_Area()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.SelectedArea = "大厅";

        Assert.Equal(2, vm.Desks.Count);
        Assert.All(vm.Desks, d => Assert.Equal("大厅", d.AreaName));
    }

    [Fact]
    public async Task Filter_By_Status()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.StatusFilter = DeskStatus.Idle;

        Assert.Equal(2, vm.Desks.Count);
        Assert.All(vm.Desks, d => Assert.Equal(DeskStatus.Idle, d.Status));
    }

    [Fact]
    public async Task Filter_Area_And_Status_Combined()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.SelectedArea = "包间";
        vm.StatusFilter = DeskStatus.Idle;

        Assert.Single(vm.Desks);
        Assert.Equal("B01", vm.Desks[0].Name);
    }

    [Fact]
    public async Task Sort_IdleFirst_By_Default()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        // 默认空闲在前
        Assert.Equal(DeskStatus.Idle, vm.Desks[0].Status);
        Assert.Equal(DeskStatus.Idle, vm.Desks[1].Status);

        vm.IdleFirst = false;

        // 切换后占用在前
        Assert.Equal(DeskStatus.Occupied, vm.Desks[0].Status);
    }

    [Fact]
    public async Task Empty_When_No_Match()
    {
        var vm = CreateVm();
        await vm.LoadAsync();

        vm.SelectedArea = "大厅";
        vm.StatusFilter = DeskStatus.Reserved; // 大厅没有预订桌

        Assert.Empty(vm.Desks);
        Assert.True(vm.IsEmpty);
    }
}
