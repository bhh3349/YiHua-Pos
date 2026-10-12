namespace YiHua.Pos.UI.Services;

/// <summary>权限码常量</summary>
public static class PermissionCode
{
    /// <summary>点单</summary>
    public const string OrderCreate = "order.create";
    /// <summary>结账</summary>
    public const string OrderSettle = "order.settle";
    /// <summary>开台</summary>
    public const string DeskOpen = "desk.open";
    /// <summary>改价</summary>
    public const string PriceModify = "price.modify";
    /// <summary>退菜</summary>
    public const string DishRefund = "dish.refund";
    /// <summary>交班</summary>
    public const string ShiftHandover = "shift.handover";
    /// <summary>改商品（商品资料维护）</summary>
    public const string ProductEdit = "product.edit";
    /// <summary>查看后厨单</summary>
    public const string KitchenView = "kitchen.view";
    /// <summary>反结账</summary>
    public const string OrderReverse = "order.reverse";
    /// <summary>重打小票</summary>
    public const string OrderReprint = "order.reprint";

    /// <summary>全部权限（店长）</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        OrderCreate, OrderSettle, DeskOpen, PriceModify, DishRefund,
        ShiftHandover, ProductEdit, KitchenView, OrderReverse, OrderReprint,
    };
}

/// <summary>
/// 权限服务：根据登录下发的权限清单判断。
/// 无权限的菜单不显示（由调用方控制可见性），而不是显示成灰色。
/// </summary>
public interface IPermissionService
{
    /// <summary>当前角色</summary>
    string CurrentRole { get; }

    /// <summary>是否有指定权限</summary>
    bool HasPermission(string code);

    /// <summary>用服务端下发的权限清单初始化</summary>
    void SetPermissions(string role, IEnumerable<string> codes);

    /// <summary>按角色设置标准权限（服务端未下发清单时的后备）</summary>
    void SetRole(string role);
}

public class PermissionService : IPermissionService
{
    private HashSet<string> _permissions = new(StringComparer.Ordinal);

    public string CurrentRole { get; private set; } = string.Empty;

    public bool HasPermission(string code) => _permissions.Contains(code);

    public void SetPermissions(string role, IEnumerable<string> codes)
    {
        CurrentRole = role;
        _permissions = new HashSet<string>(codes, StringComparer.Ordinal);
    }

    public void SetRole(string role)
    {
        CurrentRole = role;
        _permissions = new HashSet<string>(GetStandardPermissions(role), StringComparer.Ordinal);
    }

    /// <summary>
    /// 角色标准权限矩阵（4 种角色全覆盖）：
    /// 店长=全部；收银员=点单/结账/开台；服务员=点单；厨师=只看后厨单。
    /// </summary>
    public static IReadOnlyList<string> GetStandardPermissions(string role) => role switch
    {
        "店长" => PermissionCode.All,
        "收银员" => new[] { PermissionCode.OrderCreate, PermissionCode.OrderSettle, PermissionCode.DeskOpen },
        "服务员" => new[] { PermissionCode.OrderCreate },
        "厨师" => new[] { PermissionCode.KitchenView },
        _ => Array.Empty<string>(),
    };
}

/// <summary>
/// 权限要求特性（声明式标记）。
/// 用法说明：本特性仅做标记，不接入 AOP（太重）。
/// 在 ViewModel/页面初始化时手动检查：
/// <code>
/// [RequirePermission(PermissionCode.DishRefund)]
/// public void OpenRefundDialog() { ... }
/// // 调用前：
/// if (!permissionService.HasPermission(PermissionCode.DishRefund)) return; // 菜单不显示
/// </code>
/// 约定：无权限的功能入口直接不渲染，而不是禁用置灰。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    public string Code { get; }
    public RequirePermissionAttribute(string code) => Code = code;
}
