using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

internal sealed class FakeShiftRepository : IShiftRepository
{
    public ShiftEntity? Current { get; set; }
    public List<ShiftEntity> History { get; } = new();
    public ShiftEntity? LastUpdated { get; private set; }

    public Task<ShiftEntity?> GetCurrentAsync(CancellationToken ct = default) =>
        Task.FromResult(Current?.Status == ShiftStatus.Open ? Current : null);
    public Task<ShiftEntity?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Current?.Id == id ? Current : History.FirstOrDefault(h => h.Id == id));
    public Task<List<ShiftEntity>> GetHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        Task.FromResult(History.Where(h => h.StartAt >= from && h.StartAt < to).ToList());
    public Task<long> InsertAsync(ShiftEntity shift, CancellationToken ct = default)
    {
        shift.Id = 99;
        Current = shift;
        return Task.FromResult(99L);
    }
    public Task UpdateAsync(ShiftEntity shift, CancellationToken ct = default)
    {
        LastUpdated = shift;
        if (Current?.Id == shift.Id)
            Current = shift;
        return Task.CompletedTask;
    }
}

/// <summary>开班守卫测试：防重复开班</summary>
public class ShiftOpenGuardTests
{
    [Fact]
    public async Task Open_Rejects_When_Unclosed_Exists()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity { Id = 1, OperatorName = "李四", StartAt = DateTime.Now.AddHours(-8), Status = ShiftStatus.Open },
        };
        var service = new ShiftService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OpenAsync(500m, 2, "张三", "T01"));

        Assert.Contains("未关闭", ex.Message);
    }

    [Fact]
    public async Task Open_Succeeds_When_No_Current()
    {
        var repo = new FakeShiftRepository();
        var service = new ShiftService(repo);

        var shift = await service.OpenAsync(500m, 1, "张三", "T01");

        Assert.Equal(99, shift.Id);
        Assert.Equal(500m, shift.CashOpen);
        Assert.Equal(ShiftStatus.Open, shift.Status);
    }

    [Fact]
    public async Task Open_Rejects_Negative_Float()
    {
        var repo = new FakeShiftRepository();
        var service = new ShiftService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.OpenAsync(-100m, 1, "张三", "T01"));
    }
}
