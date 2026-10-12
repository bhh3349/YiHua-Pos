using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.Services.Refund;

/// <summary>
/// 后厨撤单通知：退菜时通知厨房撤单，打印退菜单。
/// 复用 Hardware 打印能力（T-106）；此处为 UI 层编排，实际打印由调用方注入。
/// 退菜单支持重复打印（厨房漏单补打）。
/// </summary>
public class KitchenNotifyService
{
    private readonly Func<RefundRecord, IReadOnlyList<OrderItem>, Task<bool>> _printFunc;

    /// <param name="printFunc">打印函数（返回是否成功；测试注入假实现）</param>
    public KitchenNotifyService(Func<RefundRecord, IReadOnlyList<OrderItem>, Task<bool>>? printFunc = null)
    {
        _printFunc = printFunc ?? ((_, _) => Task.FromResult(true));
    }

    /// <summary>发送撤单通知并打印退菜单</summary>
    public async Task<(bool Success, string? Error)> NotifyCancelAsync(
        RefundRecord record,
        IReadOnlyList<OrderItem> items,
        CancellationToken ct = default)
    {
        try
        {
            bool ok = await _printFunc(record, items).ConfigureAwait(false);
            return ok ? (true, null) : (false, "打印失败，请检查打印机后重试");
        }
        catch (Exception ex)
        {
            return (false, $"撤单通知失败：{ex.Message}");
        }
    }

    /// <summary>重复打印退菜单（补打）</summary>
    public Task<(bool Success, string? Error)> ReprintAsync(
        RefundRecord record,
        IReadOnlyList<OrderItem> items,
        CancellationToken ct = default) =>
        NotifyCancelAsync(record, items, ct);
}
