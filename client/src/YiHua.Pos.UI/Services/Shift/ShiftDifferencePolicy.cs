namespace YiHua.Pos.UI.Services.Shift;

/// <summary>差额类型</summary>
public enum DifferenceKind
{
    /// <summary>持平</summary>
    Balanced,
    /// <summary>短款（负差额，警告色）</summary>
    Shortage,
    /// <summary>溢余（正差额，中性色）</summary>
    Overage,
}

/// <summary>差异原因（下拉选择，不让收银员打字；留"其他"手输）</summary>
public enum DifferenceReason
{
    Miscounted,    // 数错了
    WrongChange,   // 找错零了
    PrivateOrder,  // 收了私单
    NoRecord,      // 抹零没记
    Stolen,        // 现金被盗
    Other,         // 其他
}

/// <summary>
/// 差异处理策略：阈值配置 + 是否需要店长确认。
/// 短款超过阈值（如 ¥100）必须店长确认。
/// </summary>
public class ShiftDifferencePolicy
{
    /// <summary>短款阈值：超过此金额需店长确认（可配置）</summary>
    public decimal ShortageThreshold { get; set; } = 100m;

    public static DifferenceKind Classify(decimal difference) =>
        difference switch
        {
            < 0 => DifferenceKind.Shortage,
            > 0 => DifferenceKind.Overage,
            _ => DifferenceKind.Balanced,
        };

    /// <summary>是否需要店长确认（短款超阈值）</summary>
    public bool RequiresManager(decimal difference) =>
        Classify(difference) == DifferenceKind.Shortage && Math.Abs(difference) > ShortageThreshold;

    public static string DescribeReason(DifferenceReason r) => r switch
    {
        DifferenceReason.Miscounted => "数错了",
        DifferenceReason.WrongChange => "找错零了",
        DifferenceReason.PrivateOrder => "收了私单",
        DifferenceReason.NoRecord => "抹零没记",
        DifferenceReason.Stolen => "现金被盗",
        _ => "其他",
    };

    /// <summary>
    /// 差额显示文本：带正负号和绝对值，不只显示 ¥-70。
    /// 如：短款 ¥70.00 / 溢余 ¥250.00 / 持平
    /// </summary>
    public static string FormatDifference(decimal difference) =>
        Classify(difference) switch
        {
            DifferenceKind.Shortage => $"短款 ¥{Math.Abs(difference):F2}",
            DifferenceKind.Overage => $"溢余 ¥{Math.Abs(difference):F2}",
            _ => "持平",
        };
}
