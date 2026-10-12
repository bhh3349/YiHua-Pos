namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 账号 DTO（设置页用的账号列表形态）。
/// 登录/鉴权复用 T-301，这里只管账号的启用/禁用展示。
/// 核心不变量：禁用 ≠ 删除，历史订单的操作人必须仍可查。
/// </summary>
public class AccountDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
}

/// <summary>
/// 账号存储接口（设置页用）。
/// </summary>
public interface IAccountStore
{
    Task<List<AccountDto>> GetAllAsync(CancellationToken ct = default);
    Task<AccountDto?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 禁用账号：只打标记，不删除记录。
    /// 历史订单通过 OperatorId 关联，禁用后仍能查到操作人姓名。
    /// </summary>
    Task DisableAsync(long id, CancellationToken ct = default);

    Task EnableAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// 内存账号存储（假实现）。
/// </summary>
public class InMemoryAccountStore : IAccountStore
{
    private readonly List<AccountDto> _accounts = new()
    {
        new() { Id = 1, Name = "张三", Role = "店长" },
        new() { Id = 2, Name = "李四", Role = "收银员" },
    };

    public Task<List<AccountDto>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_accounts.ToList());

    public Task<AccountDto?> GetByIdAsync(long id, CancellationToken ct = default)
        => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));

    public Task DisableAsync(long id, CancellationToken ct = default)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        if (account != null)
            account.IsDisabled = true; // 只打标记，不删除
        return Task.CompletedTask;
    }

    public Task EnableAsync(long id, CancellationToken ct = default)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        if (account != null)
            account.IsDisabled = false;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 操作人解析：历史订单的操作人姓名查询。
/// 即使账号已禁用，也必须能查到（财务凭证不能丢）。
/// </summary>
public static class OperatorResolver
{
    /// <summary>
    /// 按 OperatorId 解析姓名。账号禁用后仍返回姓名，不返回"未知"。
    /// </summary>
    public static string ResolveName(long operatorId, IReadOnlyList<AccountDto> accounts)
    {
        var account = accounts.FirstOrDefault(a => a.Id == operatorId);
        // 注意：不检查 IsDisabled，禁用账号的历史记录必须保留
        return account?.Name ?? $"操作员#{operatorId}";
    }
}
