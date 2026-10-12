using YiHua.Pos.UI.Services;

namespace YiHua.Pos.UI.ViewModels.Login;

/// <summary>
/// 登录主 ViewModel：编排三步流程（门店选择 → 账号密码 → 成功进主界面）。
/// 登录成功通过事件通知（不直接引用 MainWindow）。
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private LoginStep _step = LoginStep.StoreSelect;
    private bool _isOfflineMode;

    public LoginViewModel(ILoginService loginService, IPermissionService permissionService)
    {
        StoreSelect = new StoreSelectViewModel(loginService);
        LoginForm = new LoginFormViewModel(loginService);
        LoginForm.LoginSucceeded += OnLoginSucceeded;
        PermissionService = permissionService;
    }

    public StoreSelectViewModel StoreSelect { get; }
    public LoginFormViewModel LoginForm { get; }
    public IPermissionService PermissionService { get; }

    public LoginStep Step
    {
        get => _step;
        private set
        {
            SetProperty(ref _step, value);
            RaisePropertyChanged(nameof(IsStoreStep));
            RaisePropertyChanged(nameof(IsLoginStep));
        }
    }

    public bool IsStoreStep => Step == LoginStep.StoreSelect;
    public bool IsLoginStep => Step == LoginStep.LoginForm;

    /// <summary>离线模式：右上角显示离线标识</summary>
    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        private set => SetProperty(ref _isOfflineMode, value);
    }

    /// <summary>登录成功（携带完整结果，由 App 层跳转主界面）</summary>
    public event EventHandler<LoginResult>? LoginSucceeded;

    public Task InitializeAsync(CancellationToken ct = default) => StoreSelect.LoadAsync(ct);

    /// <summary>门店确认 → 进入账号密码步</summary>
    public async Task NextToLoginAsync()
    {
        var storeId = StoreSelect.EffectiveStoreId;
        if (string.IsNullOrEmpty(storeId))
            return;
        await StoreSelect.SaveSelectedStoreAsync().ConfigureAwait(false);
        Step = LoginStep.LoginForm;
    }

    /// <summary>返回门店选择</summary>
    public void BackToStoreSelect()
    {
        LoginForm.ClearPassword();
        Step = LoginStep.StoreSelect;
    }

    private void OnLoginSucceeded(object? sender, LoginResult result)
    {
        IsOfflineMode = result.IsOffline;
        if (result.User != null)
        {
            if (result.User.Permissions.Count > 0)
                PermissionService.SetPermissions(result.User.Role, result.User.Permissions);
            else
                PermissionService.SetRole(result.User.Role);
        }
        LoginSucceeded?.Invoke(this, result);
    }
}
