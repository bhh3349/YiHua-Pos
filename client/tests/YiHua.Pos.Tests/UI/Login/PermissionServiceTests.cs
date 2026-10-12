using YiHua.Pos.UI.Services;
using Xunit;

namespace YiHua.Pos.Tests.UI.Login;

/// <summary>权限服务测试：4 种角色 × 权限矩阵全覆盖</summary>
public class PermissionServiceTests
{
    [Fact]
    public void Manager_Has_All_Permissions()
    {
        var service = new PermissionService();
        service.SetRole("店长");

        foreach (var code in PermissionCode.All)
            Assert.True(service.HasPermission(code), $"店长应有权限 {code}");
    }

    [Fact]
    public void Cashier_Permissions_Match_Matrix()
    {
        var service = new PermissionService();
        service.SetRole("收银员");

        Assert.True(service.HasPermission(PermissionCode.OrderCreate));   // 点单
        Assert.True(service.HasPermission(PermissionCode.OrderSettle));   // 结账
        Assert.True(service.HasPermission(PermissionCode.DeskOpen));      // 开台
        Assert.False(service.HasPermission(PermissionCode.PriceModify));  // 不能改价
        Assert.False(service.HasPermission(PermissionCode.DishRefund));   // 不能退菜
        Assert.False(service.HasPermission(PermissionCode.ShiftHandover));// 不能交班
        Assert.False(service.HasPermission(PermissionCode.ProductEdit));  // 不能改商品
    }

    [Fact]
    public void Waiter_Can_Only_Create_Orders()
    {
        var service = new PermissionService();
        service.SetRole("服务员");

        Assert.True(service.HasPermission(PermissionCode.OrderCreate));
        Assert.False(service.HasPermission(PermissionCode.OrderSettle)); // 不能结账
        Assert.False(service.HasPermission(PermissionCode.DeskOpen));
    }

    [Fact]
    public void Chef_Can_Only_View_Kitchen()
    {
        var service = new PermissionService();
        service.SetRole("厨师");

        Assert.True(service.HasPermission(PermissionCode.KitchenView));
        Assert.False(service.HasPermission(PermissionCode.OrderCreate));
        Assert.False(service.HasPermission(PermissionCode.OrderSettle));
    }

    [Fact]
    public void SetPermissions_Uses_Server_List()
    {
        var service = new PermissionService();
        service.SetPermissions("收银员", new[] { PermissionCode.OrderCreate, PermissionCode.DishRefund });

        Assert.True(service.HasPermission(PermissionCode.DishRefund));
        Assert.False(service.HasPermission(PermissionCode.OrderSettle));
        Assert.Equal("收银员", service.CurrentRole);
    }

    [Fact]
    public void Unknown_Role_Has_No_Permissions()
    {
        var service = new PermissionService();
        service.SetRole("未知角色");

        Assert.False(service.HasPermission(PermissionCode.OrderCreate));
    }
}
