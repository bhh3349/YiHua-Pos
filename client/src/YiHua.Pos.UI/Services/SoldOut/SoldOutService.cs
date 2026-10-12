using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>
/// 沽清主流程服务。
/// 铁律：业务代码不得出现平台分支（按平台名做 if/switch 判断）。
/// 平台差异通过 ISoldOutSync 接口多态解决，新增平台只需加实现类。
/// </summary>
public sealed class SoldOutService
{
    private readonly ISoldOutRepository _repo;
    private readonly IClock _clock;
    private readonly IReadOnlyList<ISoldOutSync> _syncs;
    private readonly Action<string>? _log;

    public SoldOutService(
        ISoldOutRepository repo,
        IClock clock,
        IEnumerable<ISoldOutSync> syncs,
        Action<string>? log = null)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _syncs = syncs?.ToList() ?? new List<ISoldOutSync>();
        _log = log;
    }

    /// <summary>是否沽清</summary>
    public async Task<bool> IsSoldOutAsync(long productId, CancellationToken ct = default)
    {
        var record = await _repo.GetByProductIdAsync(productId, ct).ConfigureAwait(false);
        return record != null;
    }

    /// <summary>点单校验：已沽清拒绝</summary>
    public async Task<(bool canOrder, string? reason)> CanOrderAsync(
        long productId, CancellationToken ct = default)
    {
        bool soldOut = await IsSoldOutAsync(productId, ct).ConfigureAwait(false);
        bool ok = SoldOutCalculator.CanOrder(soldOut, out string? reason);
        return (ok, reason);
    }

    /// <summary>手工沽清单品</summary>
    public async Task SellOutAsync(
        Product product, string operatorName, CancellationToken ct = default)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        var now = _clock.Now;
        // 手工沽清：次日 09:00 自动恢复
        var restoreTime = now.Date.AddDays(1).AddHours(9);

        await _repo.SaveAsync(new SoldOutRecord
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName,
            Source = SoldOutSource.Manual,
            SoldOutTime = now,
            OperatorName = operatorName ?? string.Empty,
            ScheduledRestoreTime = restoreTime,
        }, ct).ConfigureAwait(false);

        await PushToPlatformsAsync(product.Id, soldOut: true, ct).ConfigureAwait(false);
    }

    /// <summary>恢复供应</summary>
    public async Task RestoreAsync(long productId, CancellationToken ct = default)
    {
        await _repo.RemoveAsync(productId, ct).ConfigureAwait(false);
        await PushToPlatformsAsync(productId, soldOut: false, ct).ConfigureAwait(false);
    }

    /// <summary>批量沽清（手工）</summary>
    public async Task<int> BatchSellOutAsync(
        IEnumerable<Product> products, string operatorName, CancellationToken ct = default)
    {
        int count = 0;
        foreach (var p in products)
        {
            await SellOutAsync(p, operatorName, ct).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    /// <summary>一键全部恢复</summary>
    public async Task<int> RestoreAllAsync(CancellationToken ct = default)
    {
        var all = await _repo.GetAllAsync(ct).ConfigureAwait(false);
        foreach (var r in all)
            await RestoreAsync(r.ProductId, ct).ConfigureAwait(false);
        return all.Count;
    }

    /// <summary>
    /// 推送到所有平台。
    /// 同步失败只记日志，不阻塞本地沽清。
    /// </summary>
    public async Task PushToPlatformsAsync(
        long productId, bool soldOut, CancellationToken ct = default)
    {
        foreach (var sync in _syncs)
        {
            try
            {
                var result = soldOut
                    ? await sync.PushSoldOutAsync(productId, ct).ConfigureAwait(false)
                    : await sync.PushRestoreAsync(productId, ct).ConfigureAwait(false);

                if (!result.Success && !result.NotSupported)
                    _log?.Invoke($"平台 {sync.PlatformId} 同步失败：{result.Error}");
                // NotSupported 是预期状态（未接入），不记为错误
            }
            catch (Exception ex)
            {
                // 同步异常不阻塞本地流程
                _log?.Invoke($"平台 {sync.PlatformId} 同步异常：{ex.Message}");
            }
        }
    }
}
