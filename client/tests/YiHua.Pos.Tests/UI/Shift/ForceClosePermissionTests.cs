using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部（CS0118 规避）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;


/// <summary>强制交班权限测试：仅店长</summary>
public class ForceClosePermissionTests
{
    [Fact]
    public async Task ForceClose_Records_Manager_And_Marks_Forced()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity
            {
                Id = 1, OperatorName = "李四", Status = ShiftStatus.Open,
                CashOpen = 500m, CashShould = 5000m,
            },
        };
        var service = new ShiftService(repo);
        var vm = new ForceCloseViewModel(service, repo.Current!) { ActualCash = 4950m, Confirmed = true };

        bool ok = await vm.ConfirmAsync("王店长", "店长");

        Assert.True(ok);
        Assert.Contains("[强制交班]", repo.LastUpdated!.Remark);
        Assert.Contains("王店长", repo.LastUpdated.Remark);
    }

    [Fact]
    public async Task ForceClose_NonManager_Rejected()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity { Id = 1, Status = ShiftStatus.Open, CashShould = 100m },
        };
        var service = new ShiftService(repo);
        var vm = new ForceCloseViewModel(service, repo.Current!) { ActualCash = 100m, Confirmed = true };

        bool ok = await vm.ConfirmAsync("张三", "收银员");

        Assert.False(ok);
        Assert.Contains("店长", vm.ErrorMessage);
    }

    [Fact]
    public async Task ForceClose_Requires_Double_Confirm()
    {
        var repo = new FakeShiftRepository
        {
            Current = new ShiftEntity { Id = 1, Status = ShiftStatus.Open, CashShould = 100m },
        };
        var service = new ShiftService(repo);
        var vm = new ForceCloseViewModel(service, repo.Current!) { ActualCash = 100m, Confirmed = false };

        bool ok = await vm.ConfirmAsync("王店长", "店长");

        Assert.False(ok);
        Assert.Contains("二次确认", vm.ErrorMessage);
    }
}
