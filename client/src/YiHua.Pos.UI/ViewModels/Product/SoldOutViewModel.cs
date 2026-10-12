namespace YiHua.Pos.UI.ViewModels.Product;

/// <summary>
/// 沽清管理 ViewModel：批量沽清/恢复，今日沽清列表。
/// 沽清可逆，离线生效（走 outbox，待 Domain 补字段）。
/// </summary>
public class SoldOutViewModel
{
    private readonly IProductEditService _editService;

    public SoldOutViewModel(IProductEditService editService)
    {
        _editService = editService;
    }

    /// <summary>今日沽清的商品 ID。</summary>
    public HashSet<long> SoldOutIds { get; } = new();

    /// <summary>沽清时间记录（商品ID → 沽清时间）。</summary>
    public Dictionary<long, DateTime> SoldOutTimes { get; } = new();

    /// <summary>
    /// 切换沽清状态（可测试）。
    /// 返回切换后的状态（true=沽清）。
    /// </summary>
    public bool ToggleSoldOut(long productId)
    {
        if (SoldOutIds.Contains(productId))
        {
            SoldOutIds.Remove(productId);
            SoldOutTimes.Remove(productId);
            return false;
        }
        else
        {
            SoldOutIds.Add(productId);
            SoldOutTimes[productId] = DateTime.Now;
            return true;
        }
    }

    /// <summary>
    /// 批量沽清（可测试）。
    /// </summary>
    public void BatchSoldOut(IEnumerable<long> productIds)
    {
        foreach (var id in productIds)
        {
            SoldOutIds.Add(id);
            SoldOutTimes[id] = DateTime.Now;
        }
    }

    /// <summary>
    /// 批量恢复（可测试）。
    /// </summary>
    public void BatchRestore(IEnumerable<long> productIds)
    {
        foreach (var id in productIds)
        {
            SoldOutIds.Remove(id);
            SoldOutTimes.Remove(id);
        }
    }

    /// <summary>
    /// 全部恢复。
    /// </summary>
    public void RestoreAll()
    {
        SoldOutIds.Clear();
        SoldOutTimes.Clear();
    }

    public async Task SyncToServerAsync(long productId, bool soldOut, CancellationToken ct = default)
    {
        // 离线时写入 outbox，由数据层处理（待 Domain 补 IsSoldOut 字段后接入）
        await _editService.SetSoldOutAsync(productId, soldOut, ct);
    }
}
