using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

/// <summary>
/// 账号设置测试：禁用账号后历史订单操作人仍可查。
/// </summary>
public class AccountSettingsTests
{
    [Fact]
    public async Task Disable_MarksDisabled_DoesNotDelete()
    {
        var store = new InMemoryAccountStore();
        await store.DisableAsync(2); // 禁用李四

        var account = await store.GetByIdAsync(2);
        Assert.NotNull(account); // 记录还在，没被删除
        Assert.True(account.IsDisabled);
        Assert.Equal("李四", account.Name); // 姓名保留
    }

    [Fact]
    public async Task DisabledAccount_StillInList()
    {
        var store = new InMemoryAccountStore();
        await store.DisableAsync(2);

        var all = await store.GetAllAsync();
        Assert.Equal(2, all.Count); // 禁用后列表里还有 2 人
        Assert.Contains(all, a => a.Id == 2 && a.IsDisabled);
    }

    [Fact]
    public async Task ResolveName_DisabledAccount_ReturnsName()
    {
        // 核心：禁用后历史订单仍能查到操作人姓名
        var store = new InMemoryAccountStore();
        await store.DisableAsync(2);
        var accounts = await store.GetAllAsync();

        var name = OperatorResolver.ResolveName(2, accounts);
        Assert.Equal("李四", name); // 不是"未知"，财务凭证不能丢
    }

    [Fact]
    public void ResolveName_UnknownOperator_ReturnsFallback()
    {
        var accounts = new List<AccountDto>();
        var name = OperatorResolver.ResolveName(999, accounts);
        Assert.Contains("999", name); // 兜底显示 ID，不崩
    }

    [Fact]
    public async Task Enable_RestoresAccount()
    {
        var store = new InMemoryAccountStore();
        await store.DisableAsync(2);
        await store.EnableAsync(2);

        var account = await store.GetByIdAsync(2);
        Assert.False(account!.IsDisabled);
    }

    [Fact]
    public async Task Disable_NonExistent_DoesNotThrow()
    {
        var store = new InMemoryAccountStore();
        await store.DisableAsync(999); // 不存在的 ID，不崩
        var all = await store.GetAllAsync();
        Assert.Equal(2, all.Count);
    }
}
