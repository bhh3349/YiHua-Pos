namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>沽清来源（手工 vs 定时必须在数据模型上分开）</summary>
public enum SoldOutSource
{
    /// <summary>手工沽清：老板手动标记"今天这道菜没有"，次日 09:00 自动恢复</summary>
    Manual = 1,

    /// <summary>定时沽清：按时段自动沽清（如午市菜），按时段恢复</summary>
    Scheduled = 2,
}

/// <summary>
/// 沽清记录。
/// 关键设计：手工沽清与定时沽清是两种独立记录，恢复逻辑不同，
/// 不能用一个 IsSoldOut 布尔搞定。
/// </summary>
public sealed class SoldOutRecord
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public SoldOutSource Source { get; set; }

    public DateTime SoldOutTime { get; set; } = DateTime.Now;
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>
    /// 计划恢复时间（手工：次日 09:00；定时：时段结束）。
    /// 为空表示不自动恢复。
    /// </summary>
    public DateTime? ScheduledRestoreTime { get; set; }
}

/// <summary>沽清时段（如午市 11:00-14:00）</summary>
public sealed class SoldOutTimeWindow
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>适用分类（档口），空表示全部</summary>
    public List<long> CategoryIds { get; set; } = new();
}
