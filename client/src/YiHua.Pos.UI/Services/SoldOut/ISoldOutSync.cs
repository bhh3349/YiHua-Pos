namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>沽清同步结果</summary>
public sealed class SoldOutSyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool NotSupported { get; set; }

    public static SoldOutSyncResult Ok() => new() { Success = true };
    public static SoldOutSyncResult Fail(string error) => new() { Success = false, Error = error };
    public static SoldOutSyncResult Unsupported() => new() { Success = false, NotSupported = true, Error = "该平台未接入" };
}

/// <summary>
/// 外卖平台沽清同步接口。
/// 设计：按平台实现接口，业务代码只依赖接口，不做平台分支判断。
/// 以后接美团/饿了么：新增实现类即可，业务代码零改动。
/// </summary>
public interface ISoldOutSync
{
    /// <summary>平台标识（如 "self_platform"）</summary>
    string PlatformId { get; }

    /// <summary>是否已接入</summary>
    bool IsConnected { get; }

    /// <summary>推送沽清</summary>
    Task<SoldOutSyncResult> PushSoldOutAsync(long productId, CancellationToken ct = default);

    /// <summary>推送恢复供应</summary>
    Task<SoldOutSyncResult> PushRestoreAsync(long productId, CancellationToken ct = default);
}

/// <summary>自有平台实现（本期唯一真实现）</summary>
public sealed class SelfPlatformSoldOutSync : ISoldOutSync
{
    public string PlatformId => "self_platform";
    public bool IsConnected => true;

    public Task<SoldOutSyncResult> PushSoldOutAsync(long productId, CancellationToken ct = default)
    {
        // 自有平台：本地即真相，直接成功
        return Task.FromResult(SoldOutSyncResult.Ok());
    }

    public Task<SoldOutSyncResult> PushRestoreAsync(long productId, CancellationToken ct = default)
    {
        return Task.FromResult(SoldOutSyncResult.Ok());
    }
}

/// <summary>
/// 未接入平台骨架（如美团/饿了么/抖音）。
/// 返回 NotSupported，不抛异常、不崩。
/// </summary>
public sealed class SkeletonSoldOutSync : ISoldOutSync
{
    private readonly string _platformId;

    public SkeletonSoldOutSync(string platformId)
    {
        _platformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
    }

    public string PlatformId => _platformId;
    public bool IsConnected => false;

    public Task<SoldOutSyncResult> PushSoldOutAsync(long productId, CancellationToken ct = default)
        => Task.FromResult(SoldOutSyncResult.Unsupported());

    public Task<SoldOutSyncResult> PushRestoreAsync(long productId, CancellationToken ct = default)
        => Task.FromResult(SoldOutSyncResult.Unsupported());
}
