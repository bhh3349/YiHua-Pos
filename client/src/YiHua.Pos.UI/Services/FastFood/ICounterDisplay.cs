namespace YiHua.Pos.UI.Services.FastFood;

/// <summary>叫号屏数据</summary>
public sealed class CallBoardData
{
    /// <summary>当前叫号，如 "A052"</summary>
    public string CurrentNumber { get; set; } = string.Empty;

    /// <summary>当前状态文本，如 "正在点单"</summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>等待中的号（最多显示 N 个）</summary>
    public List<string> WaitingNumbers { get; set; } = new();

    /// <summary>等待人数</summary>
    public int WaitingCount { get; set; }

    /// <summary>是否显示金额（可选）</summary>
    public bool ShowAmount { get; set; }

    public decimal CurrentAmount { get; set; }
}

/// <summary>
/// 叫号屏输出接口。
/// 实现：副屏（SecondaryCounterDisplay）/ 主屏降级（UI 层直接渲染）。
/// </summary>
public interface ICounterDisplay
{
    /// <summary>是否可用（副屏是否已连接）</summary>
    bool IsAvailable { get; }

    /// <summary>推送叫号屏数据</summary>
    void Show(CallBoardData data);
}
