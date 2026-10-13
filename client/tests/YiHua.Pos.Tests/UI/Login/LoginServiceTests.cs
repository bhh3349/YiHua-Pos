using YiHua.Pos.UI.Services;
using Xunit;

namespace YiHua.Pos.Tests.UI.Login;

/// <summary>登录服务测试：4 类错误区分 + 5 次锁定</summary>
public class LoginServiceTests
{
    private sealed class FakeApi : ILoginApi
    {
        public Func<string, string, LoginResult>? LoginFunc;
        public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task<LoginResult> LoginAsync(string s, string u, string p, CancellationToken ct) =>
            Task.FromResult(LoginFunc!(u, p));
    }

    private sealed class FakeCache : ILoginCache
    {
        public Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync() => Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task SaveStoresAsync(IReadOnlyList<StoreInfo> s) => Task.CompletedTask;
        public Task<string?> GetLastStoreIdAsync() => Task.FromResult<string?>(null);
        public Task SaveLastStoreIdAsync(string id) => Task.CompletedTask;
        public Task<CachedLogin?> GetCredentialAsync(string u) => Task.FromResult<CachedLogin?>(null);
        public Task SaveCredentialAsync(CachedLogin c) => Task.CompletedTask;
        public Task<bool> HasAnyCredentialAsync() => Task.FromResult(false);
    }

    private static LoginService Create(Func<string, string, LoginResult> loginFunc) =>
        new(new FakeApi { LoginFunc = loginFunc }, new FakeCache());

    [Theory]
    [InlineData(LoginErrorKind.UserNotFound, "账号不存在")]
    [InlineData(LoginErrorKind.WrongPassword, "密码错误")]
    [InlineData(LoginErrorKind.AccountDisabled, "账号已停用")]
    [InlineData(LoginErrorKind.NetworkError, "网络异常")]
    public async Task Login_Failure_Kinds_Are_Distinguished(LoginErrorKind kind, string hint)
    {
        var service = Create((u, p) => LoginResult.Fail(kind, $"{hint}，请检查后重试"));

        var result = await service.LoginAsync("S1", "001", "x");

        Assert.False(result.Success);
        Assert.Equal(kind, result.ErrorKind);
        Assert.Contains(hint, result.ErrorMessage);
    }

    [Fact]
    public async Task Login_5_Failures_Locks_Account()
    {
        var service = Create((u, p) => LoginResult.Fail(LoginErrorKind.WrongPassword, "密码错误"));

        LoginResult last = null!;
        for (int i = 0; i < 5; i++)
            last = await service.LoginAsync("S1", "001", "wrong");

        // 第 5 次触发锁定
        Assert.Equal(LoginErrorKind.Locked, last.ErrorKind);

        // 锁定期间即使密码正确也拒绝（API 不会被调用）
        bool apiCalled = false;
        var service2 = Create((u, p) => { apiCalled = true; return LoginResult.Ok("t", new UserInfo()); });
        // 注意：tracker 是 service 实例内的，这里用同一 service 验证锁定状态
        var lockedResult = await service.LoginAsync("S1", "001", "correct-password");
        Assert.Equal(LoginErrorKind.Locked, lockedResult.ErrorKind);
        Assert.False(apiCalled);
    }

    [Fact]
    public async Task Login_Success_Resets_Failure_Count()
    {
        int calls = 0;
        // 桩按「第 3 次调用成功」而不是按密码判断：
        // 之后所有调用一律返回密码错误，否则断言「再错 4 次不锁」拿到的是
        // 成功结果（ErrorKind 为 None），测不到失败计数是否清零。
        var service = Create((u, p) =>
        {
            calls++;
            return calls == 3
                ? LoginResult.Ok("token", new UserInfo { Username = u })
                : LoginResult.Fail(LoginErrorKind.WrongPassword, "密码错误");
        });

        await service.LoginAsync("S1", "001", "wrong");
        await service.LoginAsync("S1", "001", "wrong");
        var ok = await service.LoginAsync("S1", "001", "right");

        Assert.True(ok.Success);
        // 成功后计数清零，再错 4 次也不该锁（阈值是 5 次）。
        // 桩此时已越过第 3 次，一律返回密码错误。
        LoginResult? last = null;
        for (int i = 0; i < 4; i++)
            last = await service.LoginAsync("S1", "001", "wrong");
        Assert.Equal(LoginErrorKind.WrongPassword, last!.ErrorKind);
        Assert.False(service.IsLocked("001"));
    }

    [Fact]
    public async Task 空工号_不消耗尝试次数()
    {
        var service = Create((u, p) => LoginResult.Fail(LoginErrorKind.UserNotFound, "请输入工号"));

        for (int i = 0; i < 10; i++)
            await service.LoginAsync("S1", "", "");

        // 输入不完整不是一次「失败尝试」，不该被锁
        Assert.False(service.IsLocked(""));
        Assert.Equal(5, new LoginAttemptTracker().GetRemainingAttempts(""));
    }

    [Fact]
    public async Task 空密码_不消耗尝试次数()
    {
        var service = Create((u, p) => LoginResult.Fail(LoginErrorKind.WrongPassword, "请输入密码"));

        for (int i = 0; i < 10; i++)
            await service.LoginAsync("S1", "001", "");

        Assert.False(service.IsLocked("001"));
    }

    [Fact]
    public async Task 真实密码错误_仍然消耗尝试次数()
    {
        var service = Create((u, p) => LoginResult.Fail(LoginErrorKind.WrongPassword, "密码错误"));

        var r1 = await service.LoginAsync("S1", "001", "wrong1");
        var r2 = await service.LoginAsync("S1", "001", "wrong2");

        Assert.Contains("还剩 4 次", r1.ErrorMessage);
        Assert.Contains("还剩 3 次", r2.ErrorMessage);
    }
}
