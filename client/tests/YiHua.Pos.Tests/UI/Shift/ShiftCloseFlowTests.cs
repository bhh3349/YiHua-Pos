using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;


/// <summary>交班流程测试：完整流程</summary>
public class ShiftCloseFlowTests
{
    [Fact]
    public async Task Close_Calculates_Difference_And_Archives()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity
            {
                Id = 1, OperatorName = "张三", Status = ShiftStatus.Open,
                CashOpen = 500m, CashShould = 5700m,
            },
        };
        var service = new ShiftService(repo);

        await service.CloseAsync(1, actualCash: 5630m, DifferenceReason.WrongChange, null, "张三");

        Assert.NotNull(repo.LastUpdated);
        Assert.Equal(ShiftStatus.Closed, repo.LastUpdated!.Status);
        Assert.Equal(-70m, repo.LastUpdated.CashDiff); // 5630 - 5700 = -70 短款
        Assert.NotNull(repo.LastUpdated.EndAt);
        Assert.Contains("找错零了", repo.LastUpdated.Remark);
    }

    [Fact]
    public async Task Close_Closed_Shift_Rejected()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity { Id = 1, Status = ShiftStatus.Closed },
        };
        // GetCurrent 只返回 Open 的，这里直接测 GetById 路径
        repo.History.Add(new ShiftEntity { Id = 2, Status = ShiftStatus.Closed });
        var service = new ShiftService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CloseAsync(2, 100m, DifferenceReason.Miscounted, null, "张三"));

        Assert.Contains("已关闭", ex.Message);
    }
}
