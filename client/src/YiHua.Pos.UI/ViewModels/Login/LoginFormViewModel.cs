using YiHua.Pos.UI.Services;

namespace YiHua.Pos.UI.ViewModels.Login;

/// <summary>账号密码 ViewModel：数字键盘输入，密码可切换明文/密文，错误类型明确区分</summary>
public class LoginFormViewModel : ViewModelBase
{
    private readonly ILoginService _loginService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordVisible;
    private LoginState _state = LoginState.Idle;
    private string _errorMessage = string.Empty;
    private LoginErrorKind _errorKind = LoginErrorKind.None;
    private bool _isBusy;

    public LoginFormViewModel(ILoginService loginService)
    {
        _loginService = loginService;
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
                RaisePropertyChanged(nameof(CanLogin));
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
                RaisePropertyChanged(nameof(CanLogin));
        }
    }

    /// <summary>密码明文/密文切换</summary>
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public LoginState State
    {
        get => _state;
        private set
        {
            SetProperty(ref _state, value);
            RaisePropertyChanged(nameof(IsLoading));
            RaisePropertyChanged(nameof(HasError));
            RaisePropertyChanged(nameof(CanLogin));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public LoginErrorKind ErrorKind
    {
        get => _errorKind;
        private set => SetProperty(ref _errorKind, value);
    }

    public bool IsLoading => State == LoginState.Loading;
    public bool HasError => State == LoginState.Error;

    public bool CanLogin => !IsLoading && Username.Trim().Length > 0 && Password.Length > 0;

    /// <summary>登录成功时触发（携带 token/用户/是否离线）</summary>
    public event EventHandler<LoginResult>? LoginSucceeded;

    public async Task LoginAsync(string storeId, CancellationToken ct = default)
    {
        if (_isBusy)
            return;
        _isBusy = true;
        State = LoginState.Loading;
        ErrorMessage = string.Empty;
        ErrorKind = LoginErrorKind.None;

        try
        {
            var result = await _loginService.LoginAsync(storeId, Username, Password, ct).ConfigureAwait(false);
            if (result.Success)
            {
                State = result.IsOffline ? LoginState.Offline : LoginState.Success;
                LoginSucceeded?.Invoke(this, result);
            }
            else
            {
                State = LoginState.Error;
                ErrorKind = result.ErrorKind;
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            State = LoginState.Error;
            ErrorKind = LoginErrorKind.NetworkError;
            ErrorMessage = $"网络异常：{ex.Message}，请检查网络后重试";
        }
        finally
        {
            _isBusy = false;
        }
    }

    public void ClearPassword() => Password = string.Empty;
}
