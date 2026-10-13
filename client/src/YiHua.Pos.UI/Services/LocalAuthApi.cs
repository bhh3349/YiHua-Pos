namespace YiHua.Pos.UI.Services;

/// <summary>
/// 本地登录（独立收银版，无服务端）。
///
/// 为什么需要它：299 买断是装到商家自己电脑上的单机软件，服务端一期没上线。
/// 如果坚持「首次使用必须联网激活」，商家装完软件根本进不去 —— 软件等于不能用。
///
/// 规则：
///   本机还没有任何账号 → 这次登录即创建本地管理员账号（店长），密码哈希落库；
///   已有账号但工号对不上 → 用户不存在（不是「需要联网」，不能误导商家）；
///   工号存在 → 比对 SHA256 哈希。
/// </summary>
public sealed class LocalAuthApi : ILoginApi
{
    public const string LocalStoreId = "LOCAL";
    public const string LocalStoreName = "本店（单机版）";

    private readonly ILoginCache _cache;

    public LocalAuthApi(ILoginCache cache) => _cache = cache;

    public Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<StoreInfo>>(
            new[] { new StoreInfo { StoreId = LocalStoreId, StoreName = LocalStoreName, Address = "本机数据库" } });

    public async Task<LoginResult> LoginAsync(string storeId, string username, string password, CancellationToken ct)
    {
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;

        if (username.Length == 0)
            return LoginResult.Fail(LoginErrorKind.UserNotFound, "请输入工号");
        if (password.Length == 0)
            return LoginResult.Fail(LoginErrorKind.WrongPassword, "请输入密码");

        var cached = await _cache.GetCredentialAsync(username).ConfigureAwait(false);
        if (cached != null)
        {
            if (cached.PasswordHash != LoginService.HashPassword(password))
                return LoginResult.Fail(LoginErrorKind.WrongPassword, "密码错误，请重新输入");

            return LoginResult.Ok(string.Empty, cached.User, offline: true);
        }

        // 本机已有账号，但不是这个工号
        if (await _cache.HasAnyCredentialAsync().ConfigureAwait(false))
            return LoginResult.Fail(LoginErrorKind.UserNotFound, $"本机没有工号「{username}」，请确认后重试");

        // 首次使用：创建本地管理员
        return LoginResult.Ok(string.Empty, new UserInfo
        {
            UserId = 1,
            Username = username,
            DisplayName = username,
            Role = "店长",
        }, offline: true);
    }
}