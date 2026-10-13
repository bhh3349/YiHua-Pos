using YiHua.Pos.UI.Services;
using Xunit;

namespace YiHua.Pos.Tests.UI.Login;

/// <summary>
/// 本地登录（独立收银版）测试。
///
/// 这组测试守的是一条产品底线：**商家装完软件必须能进得去**。
/// 一期没有服务端，如果首次登录也要求「联网激活」，软件就是不能用的。
/// </summary>
public class LocalAuthApiTests
{
    private sealed class FakeCache : ILoginCache
    {
        private readonly Dictionary<string, CachedLogin> _creds = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync()
            => Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task SaveStoresAsync(IReadOnlyList<StoreInfo> s) => Task.CompletedTask;
        public Task<string?> GetLastStoreIdAsync() => Task.FromResult<string?>(null);
        public Task SaveLastStoreIdAsync(string id) => Task.CompletedTask;
        public Task<CachedLogin?> GetCredentialAsync(string u)
            => Task.FromResult(_creds.TryGetValue(u, out var c) ? c : null);
        public Task SaveCredentialAsync(CachedLogin c)
        {
            _creds[c.Username] = c;
            return Task.CompletedTask;
        }
        public Task<bool> HasAnyCredentialAsync() => Task.FromResult(_creds.Count > 0);
    }

    private static FakeCache CacheWith(params (string User, string Password, string Role)[] users)
    {
        var cache = new FakeCache();
        foreach (var (user, password, role) in users)
        {
            cache.SaveCredentialAsync(new CachedLogin
            {
                Username = user,
                PasswordHash = LoginService.HashPassword(password),
                User = new UserInfo { Username = user, DisplayName = user, Role = role },
                CachedAt = DateTime.UtcNow,
            }).GetAwaiter().GetResult();
        }
        return cache;
    }

    [Fact]
    public async Task 首次使用_本机无账号_第一次登录即创建店长账号()
    {
        var api = new LocalAuthApi(new FakeCache());

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "老板", "123456", default);

        Assert.True(result.Success);
        Assert.True(result.IsOffline);
        Assert.Equal("店长", result.User!.Role);
        Assert.Equal("老板", result.User.Username);
    }

    [Fact]
    public async Task 已有账号_工号对不上_报用户不存在而不是需要联网()
    {
        var api = new LocalAuthApi(CacheWith(("001", "123456", "收银员")));

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "999", "123456", default);

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.UserNotFound, result.ErrorKind);
        // 关键：不能误导成「首次使用需要联网激活」
        Assert.DoesNotContain("联网", result.ErrorMessage);
        Assert.Contains("999", result.ErrorMessage);
    }

    [Fact]
    public async Task 已有账号_密码错误_报密码错误()
    {
        var api = new LocalAuthApi(CacheWith(("001", "123456", "收银员")));

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "001", "654321", default);

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.WrongPassword, result.ErrorKind);
    }

    [Fact]
    public async Task 已有账号_密码正确_登录成功且沿用原角色()
    {
        var api = new LocalAuthApi(CacheWith(("001", "123456", "收银员")));

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "001", "123456", default);

        Assert.True(result.Success);
        Assert.Equal("收银员", result.User!.Role);
    }

    [Fact]
    public async Task 工号大小写与空格_视为同一账号()
    {
        var api = new LocalAuthApi(CacheWith(("001", "123456", "收银员")));

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "  001  ", "123456", default);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task 工号为空_拒绝()
    {
        var api = new LocalAuthApi(new FakeCache());

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "   ", "123456", default);

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.UserNotFound, result.ErrorKind);
    }

    [Fact]
    public async Task 密码为空_拒绝()
    {
        var api = new LocalAuthApi(new FakeCache());

        var result = await api.LoginAsync(LocalAuthApi.LocalStoreId, "老板", "", default);

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.WrongPassword, result.ErrorKind);
    }

    [Fact]
    public async Task 门店列表_单机版返回本地门店()
    {
        var api = new LocalAuthApi(new FakeCache());

        var stores = await api.GetStoresAsync(default);

        Assert.Single(stores);
        Assert.Equal(LocalAuthApi.LocalStoreId, stores[0].StoreId);
    }
}