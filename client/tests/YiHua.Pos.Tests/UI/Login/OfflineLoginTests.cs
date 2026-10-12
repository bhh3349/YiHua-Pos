using YiHua.Pos.UI.Services;
using Xunit;

namespace YiHua.Pos.Tests.UI.Login;

/// <summary>离线登录测试：有缓存 → 允许进（标记离线）；无缓存 → 拒绝并指引联网</summary>
public class OfflineLoginTests
{
    private sealed class NetworkDownApi : ILoginApi
    {
        public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct) =>
            throw new HttpRequestException("网络不可达");
        public Task<LoginResult> LoginAsync(string s, string u, string p, CancellationToken ct) =>
            throw new HttpRequestException("网络不可达");
    }

    private sealed class FakeCache : ILoginCache
    {
        private readonly Dictionary<string, CachedLogin> _creds = new();
        public void Seed(CachedLogin c) => _creds[c.Username] = c;

        public Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync() =>
            Task.FromResult<IReadOnlyList<StoreInfo>>(new[] { new StoreInfo { StoreId = "S1", StoreName = "缓存门店" } });
        public Task SaveStoresAsync(IReadOnlyList<StoreInfo> s) => Task.CompletedTask;
        public Task<string?> GetLastStoreIdAsync() => Task.FromResult<string?>("S1");
        public Task SaveLastStoreIdAsync(string id) => Task.CompletedTask;
        public Task<CachedLogin?> GetCredentialAsync(string u) =>
            Task.FromResult(_creds.TryGetValue(u, out var c) ? c : null);
        public Task SaveCredentialAsync(CachedLogin c) { _creds[c.Username] = c; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Offline_With_Cache_Allows_Login_Marked_Offline()
    {
        var cache = new FakeCache();
        cache.Seed(new CachedLogin
        {
            Username = "001",
            PasswordHash = LoginService.HashPassword("123456"),
            User = new UserInfo { Username = "001", DisplayName = "张三", Role = "收银员" },
        });
        var service = new LoginService(new NetworkDownApi(), cache);

        var result = await service.LoginAsync("S1", "001", "123456");

        Assert.True(result.Success);
        Assert.True(result.IsOffline);
        Assert.Equal("张三", result.User!.DisplayName);
    }

    [Fact]
    public async Task Offline_Without_Cache_Requires_Network_Activation()
    {
        var service = new LoginService(new NetworkDownApi(), new FakeCache());

        var result = await service.LoginAsync("S1", "999", "123456");

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.FirstUseNeedNetwork, result.ErrorKind);
        Assert.Contains("联网", result.ErrorMessage);
    }

    [Fact]
    public async Task Offline_Wrong_Password_Rejected()
    {
        var cache = new FakeCache();
        cache.Seed(new CachedLogin
        {
            Username = "001",
            PasswordHash = LoginService.HashPassword("123456"),
            User = new UserInfo { Username = "001", Role = "收银员" },
        });
        var service = new LoginService(new NetworkDownApi(), cache);

        var result = await service.LoginAsync("S1", "001", "wrong");

        Assert.False(result.Success);
        Assert.Equal(LoginErrorKind.WrongPassword, result.ErrorKind);
    }

    [Fact]
    public async Task GetStores_Offline_Falls_Back_To_Cache()
    {
        var service = new LoginService(new NetworkDownApi(), new FakeCache());

        var stores = await service.GetStoresAsync();

        Assert.Single(stores);
        Assert.Equal("缓存门店", stores[0].StoreName);
    }
}
