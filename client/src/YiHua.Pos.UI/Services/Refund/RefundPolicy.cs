namespace YiHua.Pos.UI.Services.Refund;

/// <summary>退款策略：阈值与权限规则（可配置）</summary>
public interface IRefundPolicy
{
    /// <summary>整单退单超阈值需店长授权</summary>
    decimal ManagerAuthThreshold { get; }

    /// <summary>是否需要店长授权</summary>
    bool RequiresManagerAuth(RefundType type, decimal amount, string operatorRole);

    /// <summary>角色是否允许执行该操作</summary>
    (bool Allowed, string? DenyReason) CanPerform(RefundType type, string operatorRole);
}

/// <summary>
/// 默认策略：
/// - 店长：可退单、可冲正
/// - 收银员：可退菜、可部分退款；整单退单超 ¥200 需店长授权
/// </summary>
public class DefaultRefundPolicy : IRefundPolicy
{
    public decimal ManagerAuthThreshold { get; set; } = 200m;

    public bool RequiresManagerAuth(RefundType type, decimal amount, string operatorRole)
    {
        if (operatorRole == "店长")
            return false;
        // 整单退单超阈值需店长授权
        if (type == RefundType.Order && amount > ManagerAuthThreshold)
            return true;
        return false;
    }

    public (bool Allowed, string? DenyReason) CanPerform(RefundType type, string operatorRole)
    {
        return (type, operatorRole) switch
        {
            // 店长：全部可做
            (_, "店长") => (true, null),
            // 收银员：可退菜、部分退款；整单退单需授权（由 RequiresManagerAuth 判断）；可冲正未结账单
            (RefundType.Item, "收银员") => (true, null),
            (RefundType.Order, "收银员") => (true, null), // 超阈值时需授权，按钮不置灰但弹授权窗
            (RefundType.Void, "收银员") => (true, null),
            // 服务员：只能退菜（且需在权限范围内）
            (RefundType.Item, "服务员") => (true, null),
            (RefundType.Order, "服务员") => (false, "服务员不能整单退单，请联系收银员或店长"),
            (RefundType.Void, "服务员") => (false, "服务员不能冲正，请联系收银员或店长"),
            // 厨师：不能做任何退款
            (_, "厨师") => (false, "厨师没有退款权限"),
            _ => (false, "未知角色，没有退款权限"),
        };
    }
}
