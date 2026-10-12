using YiHua.Pos.UI.Services;
using YiHua.Pos.UI.ViewModels.Login;
using Xunit;

namespace YiHua.Pos.Tests.UI.Login;

/// <summary>登录状态机测试：状态流转正确</summary>
public class LoginStateMachineTests
{
    private sealed class FakeApi : ILoginApi
    {
        public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoreInfo>>(new[] { new StoreInfo { StoreId = "S1", StoreName = "测试店" } });
        public Task<LoginResult> LoginAsync(string s, string u, string p, CancellationToken ct) =>
            Task.FromResult(LoginResult.Ok("token", new UserInfo { Username = u, Role = "收银员" }));
    }

    private sealed class FakeCache : ILoginCache
    {
        public Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync() => Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task SaveStoresAsync(IReadOnlyList<StoreInfo> s) => Task.CompletedTask;
        public Task<string?> GetLastStoreIdAsync() => Task.FromResult<string?>(null);
        public Task SaveLastStoreIdAsync(string id) => Task.CompletedTask;
        public Task<CachedLogin?> GetCredentialAsync(string u) => Task.FromResult<CachedLogin?>(null);
        public Task SaveCredentialAsync(CachedLogin c) => Task.CompletedTask;
    }

    [Fact]
    public async Task LoginForm_Success_Transitions_To_Success()
    {
        var vm = new LoginFormViewModel(new LoginService(new FakeApi(), new FakeCache()));
        vm.Username = "001";
        vm.Password = "123456";

        Assert.Equal(LoginState.Idle, vm.State);
        await vm.LoginAsync("S1");

        Assert.Equal(LoginState.Success, vm.State);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoginForm_Failure_Transitions_To_Error_With_Kind()
    {
        var api = new FailingApi(LoginErrorKind.WrongPassword, "密码错误，请重新输入");
        var vm = new LoginFormViewModel(new LoginService(api, new FakeCache()));
        vm.Username = "001";
        vm.Password = "wrong";

        await vm.LoginAsync("S1");

        Assert.Equal(LoginState.Error, vm.State);
        Assert.True(vm.HasError);
        Assert.Equal(LoginErrorKind.WrongPassword, vm.ErrorKind);
        Assert.Contains("密码错误", vm.ErrorMessage);
    }

    [Fact]
    public async Task StoreSelect_Empty_Transitions_To_Empty()
    {
        var vm = new StoreSelectViewModel(new LoginService(new EmptyStoreApi(), new FakeCache()));

        await vm.LoadAsync();

        Assert.Equal(LoginState.Empty, vm.State);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task LoginViewModel_Step_Transitions()
    {
        var service = new LoginService(new FakeApi(), new FakeCache());
        var vm = new LoginViewModel(service, new PermissionService());

        Assert.True(vm.IsStoreStep);
        vm.StoreSelect.ManualStoreCode = "S99";
        await vm.NextToLoginAsync();

        Assert.True(vm.IsLoginStep);
        vm.BackToStoreSelect();
        Assert.True(vm.IsStoreStep);
    }

    private sealed class FailingApi : ILoginApi
    {
        private readonly LoginErrorKind _kind;
        private readonly string _msg;
        public FailingApi(LoginErrorKind kind, string msg) { _kind = kind; _msg = msg; }
        public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task<LoginResult> LoginAsync(string s, string u, string p, CancellationToken ct) =>
            Task.FromResult(LoginResult.Fail(_kind, _msg));
    }

    private sealed class EmptyStoreApi : ILoginApi
    {
        public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoreInfo>>(Array.Empty<StoreInfo>());
        public Task<LoginResult> LoginAsync(string s, string u, string p, CancellationToken ct) =>
            Task.FromResult(LoginResult.Fail(LoginErrorKind.NetworkError, "x"));
    }
}
