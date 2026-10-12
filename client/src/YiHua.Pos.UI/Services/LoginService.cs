using System.Security.Cryptography;
using System.Text;

namespace YiHua.Pos.UI.Services;

/// <summary>服务端登录 API 抽象（测试注入假实现，不依赖真实服务端）</summary>
public interface ILoginApi
{
    Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct);
    Task<LoginResult> LoginAsync(string storeId, string username, string password, CancellationToken ct);
}

/// <summary>本地登录缓存抽象（门店列表、上次门店、用户凭据）</summary>
public interface ILoginCache
{
    Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync();
    Task SaveStoresAsync(IReadOnlyList<StoreInfo> stores);
    Task<string?> GetLastStoreIdAsync();
    Task SaveLastStoreIdAsync(string storeId);
    Task<CachedLogin?> GetCredentialAsync(string username);
    Task SaveCredentialAsync(CachedLogin credential);
}

/// <summary>
/// 登录失败追踪：连续失败 5 次锁定 5 分钟，锁定期间即使密码正确也拒绝。
/// 纯逻辑，可单测。
/// </summary>
public class LoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, (int Count, DateTime? LockedUntil)> _state = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTime> _now;

    public LoginAttemptTracker(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    public bool IsLocked(string username)
    {
        if (!_state.TryGetValue(username, out var s) || s.LockedUntil == null)
            return false;
        if (s.LockedUntil <= _now())
        {
            _state.Remove(username);
            return false;
        }
        return true;
    }

    /// <summary>剩余锁定时间（未锁定返回 Zero）</summary>
    public TimeSpan GetRemainingLock(string username)
    {
        if (!_state.TryGetValue(username, out var s) || s.LockedUntil == null)
            return TimeSpan.Zero;
        var remain = s.LockedUntil.Value - _now();
        return remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
    }

    /// <summary>剩余可尝试次数</summary>
    public int GetRemainingAttempts(string username)
    {
        if (!_state.TryGetValue(username, out var s))
            return MaxFailures;
        return Math.Max(0, MaxFailures - s.Count);
    }

    public void RecordFailure(string username)
    {
        _state.TryGetValue(username, out var s);
        int count = s.Count + 1;
        DateTime? lockedUntil = count >= MaxFailures ? _now() + LockDuration : null;
        _state[username] = (count, lockedUntil);
    }

    public void Reset(string username) => _state.Remove(username);
}

public interface ILoginService
{
    string? LastStoreId { get; }
    Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct = default);
    Task SaveLastStoreIdAsync(string storeId);
    Task<LoginResult> LoginAsync(string storeId, string username, string password, CancellationToken ct = default);
}

/// <summary>
/// 登录服务：先试服务端，网络不可达时读本地缓存走离线登录。
/// 本地有缓存 → 允许进并标记离线模式；无缓存 → 提示首次使用需联网激活。
/// </summary>
public class LoginService : ILoginService
{
    private readonly ILoginApi _api;
    private readonly ILoginCache _cache;
    private readonly LoginAttemptTracker _tracker = new();

    public LoginService(ILoginApi api, ILoginCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public string? LastStoreId => _cache.GetLastStoreIdAsync().GetAwaiter().GetResult();

    public async Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken ct = default)
    {
        try
        {
            var stores = await _api.GetStoresAsync(ct).ConfigureAwait(false);
            await _cache.SaveStoresAsync(stores).ConfigureAwait(false);
            return stores;
        }
        catch
        {
            // 离线：用本地缓存的上次门店列表
            return await _cache.GetCachedStoresAsync().ConfigureAwait(false);
        }
    }

    public Task SaveLastStoreIdAsync(string storeId) => _cache.SaveLastStoreIdAsync(storeId);

    public async Task<LoginResult> LoginAsync(string storeId, string username, string password, CancellationToken ct = default)
    {
        username = username.Trim();
        if (_tracker.IsLocked(username))
        {
            var remain = _tracker.GetRemainingLock(username);
            return LoginResult.Fail(LoginErrorKind.Locked,
                $"账号已锁定，请 {Math.Ceiling(remain.TotalMinutes)} 分钟后再试");
        }

        LoginResult result;
        try
        {
            result = await _api.LoginAsync(storeId, username, password, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNetworkError(ex))
        {
            return await LoginOfflineAsync(username, password).ConfigureAwait(false);
        }

        if (result.Success)
        {
            _tracker.Reset(username);
            if (result.User != null)
            {
                await _cache.SaveCredentialAsync(new CachedLogin
                {
                    Username = username,
                    PasswordHash = HashPassword(password),
                    User = result.User,
                    CachedAt = DateTime.UtcNow,
                }).ConfigureAwait(false);
            }
        }
        else if (result.ErrorKind is LoginErrorKind.WrongPassword or LoginErrorKind.UserNotFound)
        {
            _tracker.RecordFailure(username);
            int remain = _tracker.GetRemainingAttempts(username);
            if (remain == 0)
                return LoginResult.Fail(LoginErrorKind.Locked, "连续失败 5 次，账号已锁定 5 分钟");
            result.ErrorMessage += $"（还剩 {remain} 次机会）";
        }

        return result;
    }

    private async Task<LoginResult> LoginOfflineAsync(string username, string password)
    {
        var cached = await _cache.GetCredentialAsync(username).ConfigureAwait(false);
        if (cached == null)
        {
            return LoginResult.Fail(LoginErrorKind.FirstUseNeedNetwork,
                "首次使用需要联网激活，请连接网络后重试");
        }
        if (cached.PasswordHash != HashPassword(password))
        {
            _tracker.RecordFailure(username);
            return LoginResult.Fail(LoginErrorKind.WrongPassword, "密码错误，请重新输入");
        }
        return LoginResult.Ok(string.Empty, cached.User, offline: true);
    }

    private static bool IsNetworkError(Exception ex) =>
        ex is HttpRequestException || ex is TaskCanceledException || ex is TimeoutException;

    /// <summary>密码哈希（SHA256，缓存比对用）</summary>
    public static string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
