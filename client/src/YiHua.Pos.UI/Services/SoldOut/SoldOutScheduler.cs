namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>
/// 沽清定时调度器。
/// 职责：到点自动恢复供应（手工沽清次日 09:00，定时沽清按时段）。
/// 时钟注入，可测试。
/// </summary>
public sealed class SoldOutScheduler
{
    private readonly ISoldOutRepository _repo;
    private readonly IClock _clock;

    /// <summary>每日恢复时间（默认 09:00，说人话："每天 09:00 恢复供应"）</summary>
    public TimeSpan DailyResetTime { get; set; } = new TimeSpan(9, 0, 0);

    public SoldOutScheduler(ISoldOutRepository repo, IClock clock)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 执行一次定时检查：恢复到期的沽清记录。
    /// 返回本次恢复的商品ID列表。
    /// </summary>
    public async Task<List<long>> RunOnceAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var all = await _repo.GetAllAsync(ct).ConfigureAwait(false);
        var restored = new List<long>();

        foreach (var record in all)
        {
            if (SoldOutCalculator.ShouldRestore(record, now, DailyResetTime))
            {
                await _repo.RemoveAsync(record.ProductId, ct).ConfigureAwait(false);
                restored.Add(record.ProductId);
            }
        }

        return restored;
    }

    /// <summary>下次恢复时间（UI 显示用："每天 09:00 恢复供应"）</summary>
    public string DailyResetText =>
        $"每天 {DailyResetTime.Hours:D2}:{DailyResetTime.Minutes:D2} 恢复供应";
}
