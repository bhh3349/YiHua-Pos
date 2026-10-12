using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Desk;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;
using Xunit;

namespace YiHua.Pos.Tests.UI.Desk;

internal sealed class FakeDeskRepository : IDeskRepository
{
    public List<DeskArea> Areas { get; } = new();
    public List<DeskEntity> Desks { get; } = new();

    public Task<List<DeskArea>> GetAreasAsync(CancellationToken ct = default) => Task.FromResult(Areas.ToList());
    public Task<List<DeskEntity>> GetDesksAsync(long? areaId = null, CancellationToken ct = default) =>
        Task.FromResult(areaId == null ? Desks.ToList() : Desks.Where(d => d.AreaId == areaId).ToList());
    public Task<DeskEntity?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Desks.FirstOrDefault(d => d.Id == id));
    public Task UpdateAsync(DeskEntity desk, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReplaceAllAsync(IEnumerable<DeskArea> areas, IEnumerable<DeskEntity> desks, CancellationToken ct = default) =>
        Task.CompletedTask;
}

/// <summary>换台测试：明细搬移、人数继承、源桌变空闲</summary>
public class DeskChangeDeskTests
{
    [Fact]
    public async Task Change_Moves_Everything_To_Target()
    {
        var repo = new FakeDeskRepository();
        var source = new DeskEntity
        {
            Id = 1, Name = "A01", Status = DeskStatus.Occupied,
            GuestCount = 4, CurrentOrderId = 100, CurrentAmount = 128.00m,
            OpenTime = DateTime.Now.AddMinutes(-25),
        };
        var target = new DeskEntity { Id = 2, Name = "A02", Status = DeskStatus.Idle };
        var vm = new DeskChangeViewModel(repo, source) { TargetDesk = target };

        bool ok = await vm.ConfirmAsync(sourceHasUnsettledItems: false);

        Assert.True(ok);
        Assert.Equal(DeskStatus.Occupied, target.Status);
        Assert.Equal(4, target.GuestCount);           // 人数继承
        Assert.Equal(100, target.CurrentOrderId);     // 订单搬移
        Assert.Equal(128.00m, target.CurrentAmount);
        Assert.Equal(DeskStatus.Idle, source.Status); // 源桌变空闲
        Assert.Equal(0, source.GuestCount);
    }

    [Fact]
    public async Task Change_Rejects_When_Target_Occupied()
    {
        var repo = new FakeDeskRepository();
        var source = new DeskEntity { Id = 1, Name = "A01", Status = DeskStatus.Occupied };
        var target = new DeskEntity { Id = 2, Name = "A02", Status = DeskStatus.Occupied };
        var vm = new DeskChangeViewModel(repo, source) { TargetDesk = target };

        bool ok = await vm.ConfirmAsync(false);

        Assert.False(ok);
        Assert.Contains("A02", vm.ErrorMessage);
    }

    [Fact]
    public async Task Change_Rejects_Unsettled_Source()
    {
        var repo = new FakeDeskRepository();
        var source = new DeskEntity { Id = 1, Name = "A01", Status = DeskStatus.Occupied };
        var target = new DeskEntity { Id = 2, Name = "A02", Status = DeskStatus.Idle };
        var vm = new DeskChangeViewModel(repo, source) { TargetDesk = target };

        bool ok = await vm.ConfirmAsync(sourceHasUnsettledItems: true);

        Assert.False(ok);
        Assert.Contains("清台", vm.ErrorMessage);
    }
}

