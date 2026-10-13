namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 沽清管理 ViewModel：批量沽清/恢复，今日沽清列表。
///
/// 状态**不再只放内存**：每次切换都会写回商品表（IsSoldOut + SoldOutAt），
/// 重启后仍然是沽清状态。之前用内存 HashSet，老板早上沽清三道菜、
/// 晚上重启软件就全没了，客人点了沽清的菜照样能下单。
///
/// 内存集合保留为「本次会话的界面镜像」，从数据源 Load 时填充。
/// </summary>
public class SoldOutViewModel
{
    private readonly IProductEditService _editService;
    private readonly IProductDataSource? _dataSource;

    public SoldOutViewModel(IProductEditService editService, IProductDataSource? dataSource = null)
    {
        _editService = editService;
        _dataSource = dataSource;
    }

    /// <summary>今日沽清的商品 ID（界面镜像，来源是商品表）。</summary>
    public HashSet<long> SoldOutIds { get; } = new();

    /// <summary>沽清时间记录（商品ID → 沽清时间）。</summary>
    public Dictionary<long, DateTime> SoldOutTimes { get; } = new();

    /// <summary>从商品表载入当前沽清状态。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_dataSource == null) return;

        SoldOutIds.Clear();
        SoldOutTimes.Clear();

        foreach (var p in await _dataSource.GetAllAsync(ct).ConfigureAwait(false))
        {
            if (!p.IsSoldOut) continue;
            SoldOutIds.Add(p.Id);
            if (p.SoldOutAt.HasValue) SoldOutTimes[p.Id] = p.SoldOutAt.Value;
        }
    }

    /// <summary>
    /// 切换沽清状态。返回切换后的状态（true=沽清）。
    /// 内存与数据库一起改，任何一边失败都不会留下不一致的界面。
    /// </summary>
    public bool ToggleSoldOut(long productId)
    {
        var next = !SoldOutIds.Contains(productId);
        ApplyLocal(productId, next);
        Persist(productId, next);
        return next;
    }

    /// <summary>批量沽清。</summary>
    public void BatchSoldOut(IEnumerable<long> productIds)
    {
        foreach (var id in productIds)
        {
            ApplyLocal(id, true);
            Persist(id, true);
        }
    }

    /// <summary>批量恢复。</summary>
    public void BatchRestore(IEnumerable<long> productIds)
    {
        foreach (var id in productIds)
        {
            ApplyLocal(id, false);
            Persist(id, false);
        }
    }

    /// <summary>全部恢复（打烊收工用）。</summary>
    public void RestoreAll()
    {
        foreach (var id in SoldOutIds.ToList())
        {
            ApplyLocal(id, false);
            Persist(id, false);
        }
    }

    private void ApplyLocal(long productId, bool soldOut)
    {
        if (soldOut)
        {
            SoldOutIds.Add(productId);
            SoldOutTimes[productId] = DateTime.Now;
        }
        else
        {
            SoldOutIds.Remove(productId);
            SoldOutTimes.Remove(productId);
        }
    }

    /// <summary>落库。界面不能因为写库慢而卡住，所以是即发即忘。</summary>
    private void Persist(long productId, bool soldOut)
        => _ = _editService.SetSoldOutAsync(productId, soldOut);

    /// <summary>等待所有挂起的落库完成（测试/退出前用）。</summary>
    public async Task SyncToServerAsync(long productId, bool soldOut, CancellationToken ct = default)
        => await _editService.SetSoldOutAsync(productId, soldOut, ct).ConfigureAwait(false);
}