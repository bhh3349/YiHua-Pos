// MUSE-2 · T-105 电子秤协议
// 电子秤状态枚举（对应《协议规范-电子秤.md》1.4 节）

namespace YiHua.Pos.Hardware.Scales;

/// <summary>电子秤状态</summary>
public enum ScaleStatus
{
    /// <summary>无数据</summary>
    Empty = 0,

    /// <summary>稳定（可取数）</summary>
    Stable = 1,

    /// <summary>晃动中（不可取数）</summary>
    Weighting = 2,

    /// <summary>超载</summary>
    OverLoad = 3,

    /// <summary>空秤（重量为 0）</summary>
    UnWeight = 4,

    /// <summary>负重量</summary>
    NegativeWeight = 5,

    /// <summary>解析错误</summary>
    Error = 6,
}
