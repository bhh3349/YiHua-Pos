using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>
/// 沽清计算器（纯逻辑）。
/// 注意：这是沽清，不是进销存——不做库存数量、出入库、盘点。
/// </summary>
public static class SoldOutCalculator
{
    /// <summary>是否可点单（已沽清 → 拒绝）</summary>
    public static bool CanOrder(bool isSoldOut, out string? reason)
    {
        reason = null;
        if (isSoldOut)
        {
            reason = "该商品已沽清";
            return false;
        }
        return true;
    }

    /// <summary>按档口筛选商品ID</summary>
    public static List<long> FilterByCategory(IEnumerable<Product> all, long categoryId)
    {
        if (all == null) return new List<long>();
        return all.Where(p => p.CategoryId == categoryId).Select(p => p.Id).ToList();
    }

    /// <summary>按关键字筛选商品ID（无匹配返回空，不报错）</summary>
    public static List<long> FilterByKeyword(IEnumerable<Product> all, string keyword)
    {
        if (all == null || string.IsNullOrWhiteSpace(keyword))
            return new List<long>();
        var kw = keyword.Trim();
        return all.Where(p => p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                  .Select(p => p.Id).ToList();
    }

    /// <summary>
    /// 判断沽清记录是否该恢复。
    /// 规则：只有定时沽清（Scheduled）且到了 ScheduledRestoreTime 才自动恢复；
    /// 手工沽清（Manual）按次日 09:00 恢复（由调用方传入 dailyReset 时间判断）。
    /// </summary>
    public static bool ShouldRestore(SoldOutRecord record, DateTime now, TimeSpan dailyResetTime)
    {
        if (record == null) return false;

        if (record.Source == SoldOutSource.Scheduled)
        {
            // 定时沽清：到了计划恢复时间就恢复
            return record.ScheduledRestoreTime.HasValue && now >= record.ScheduledRestoreTime.Value;
        }

        // 手工沽清：次日 dailyResetTime（如 09:00）恢复
        // 计算"上一个恢复点"：如果 now 还没到今天的恢复点，用昨天的
        var todayReset = now.Date.Add(dailyResetTime);
        var lastReset = now >= todayReset ? todayReset : todayReset.AddDays(-1);
        // 沽清时间在上一个恢复点之前 → 该恢复
        return record.SoldOutTime < lastReset;
    }

    /// <summary>判断当前时间是否在沽清时段内</summary>
    public static bool InSoldOutWindow(TimeSpan now, IEnumerable<SoldOutTimeWindow> windows)
    {
        if (windows == null) return false;
        foreach (var w in windows)
        {
            if (w.Start <= w.End)
            {
                // 同一天内，如 11:00-14:00
                if (now >= w.Start && now < w.End) return true;
            }
            else
            {
                // 跨天，如 22:00-02:00
                if (now >= w.Start || now < w.End) return true;
            }
        }
        return false;
    }

    /// <summary>判断某分类当前是否在沽清时段内</summary>
    public static bool InSoldOutWindowForCategory(
        TimeSpan now, long categoryId, IEnumerable<SoldOutTimeWindow> windows)
    {
        if (windows == null) return false;
        var applicable = windows.Where(w =>
            w.CategoryIds.Count == 0 || w.CategoryIds.Contains(categoryId));
        return InSoldOutWindow(now, applicable);
    }
}
