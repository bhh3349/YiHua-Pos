using System.Text.Json;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 本地登录缓存持久化实现。
/// 凭据存 SHA256 哈希，不存明文；全部落在 DictEntry 表里，重启后保留。
/// </summary>
internal sealed class LocalLoginCache : ILoginCache
{
    private const string StoresKey = "login:stores";
    private const string LastStoreKey = "login:last-store";
    private const string CredentialPrefix = "login:credential:";
    private const string AccountsKey = "login:accounts";

    private readonly IDictionaryRepository _dict;

    public LocalLoginCache(IDictionaryRepository dict) => _dict = dict;

    public async Task<IReadOnlyList<StoreInfo>> GetCachedStoresAsync()
    {
        var json = await _dict.GetAsync(StoresKey).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json)
            ? Array.Empty<StoreInfo>()
            : JsonSerializer.Deserialize<List<StoreInfo>>(json) ?? new();
    }

    public Task SaveStoresAsync(IReadOnlyList<StoreInfo> stores)
        => _dict.SetAsync(StoresKey, JsonSerializer.Serialize(stores));

    public Task<string?> GetLastStoreIdAsync() => _dict.GetAsync(LastStoreKey);

    public Task SaveLastStoreIdAsync(string storeId) => _dict.SetAsync(LastStoreKey, storeId);

    public async Task<CachedLogin?> GetCredentialAsync(string username)
    {
        var json = await _dict.GetAsync(CredentialPrefix + Normalize(username)).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CachedLogin>(json);
    }

    public async Task SaveCredentialAsync(CachedLogin credential)
    {
        await _dict.SetAsync(CredentialPrefix + Normalize(credential.Username),
            JsonSerializer.Serialize(credential)).ConfigureAwait(false);

        // 维护账号名索引：判断「本机是否已有任何账号」不能靠扫描键前缀，
        // 显式记一份清单，避免依赖存储实现的前缀查询能力。
        var accounts = await GetAccountsAsync().ConfigureAwait(false);
        if (!accounts.Contains(Normalize(credential.Username), StringComparer.Ordinal))
        {
            accounts.Add(Normalize(credential.Username));
            await _dict.SetAsync(AccountsKey, JsonSerializer.Serialize(accounts)).ConfigureAwait(false);
        }
    }

    public async Task<bool> HasAnyCredentialAsync()
        => (await GetAccountsAsync().ConfigureAwait(false)).Count > 0;

    private async Task<List<string>> GetAccountsAsync()
    {
        var json = await _dict.GetAsync(AccountsKey).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private static string Normalize(string username) => (username ?? string.Empty).Trim().ToLowerInvariant();
}