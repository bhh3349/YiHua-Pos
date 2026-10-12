namespace YiHua.Pos.UI.Services;

/// <summary>门店信息</summary>
public class StoreInfo
{
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>用户信息</summary>
public class UserInfo
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>角色：店长 / 收银员 / 服务员 / 厨师</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>权限码清单（服务端下发）</summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>登录失败的具体原因（必须区分，不能都报"登录失败"）</summary>
public enum LoginErrorKind
{
    None,
    /// <summary>账号不存在</summary>
    UserNotFound,
    /// <summary>密码错误</summary>
    WrongPassword,
    /// <summary>账号已停用</summary>
    AccountDisabled,
    /// <summary>网络异常</summary>
    NetworkError,
    /// <summary>连续失败被锁定</summary>
    Locked,
    /// <summary>首次使用需要联网激活（本地无缓存）</summary>
    FirstUseNeedNetwork,
}

/// <summary>登录结果</summary>
public class LoginResult
{
    public bool Success { get; set; }
    public LoginErrorKind ErrorKind { get; set; } = LoginErrorKind.None;

    /// <summary>给收银员看的错误说明：发生了什么 + 怎么修，不道歉不含糊</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public UserInfo? User { get; set; }

    /// <summary>是否为离线模式登录</summary>
    public bool IsOffline { get; set; }

    public static LoginResult Ok(string token, UserInfo user, bool offline = false) =>
        new() { Success = true, Token = token, User = user, IsOffline = offline };

    public static LoginResult Fail(LoginErrorKind kind, string message) =>
        new() { Success = false, ErrorKind = kind, ErrorMessage = message };
}

/// <summary>本地缓存的登录凭据（离线登录用；存密码哈希，不存明文）</summary>
public class CachedLogin
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserInfo User { get; set; } = new();
    public DateTime CachedAt { get; set; }
}
