using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.Controls.Display;
using YiHua.Pos.UI.Controls.Lists;
using Xunit;

namespace YiHua.Pos.Tests.UI;

/// <summary>
/// 桌台卡片状态映射 + 订单行小计逻辑测试。
/// </summary>
public class DeskCardStateTests
{
    [Theory]
    [InlineData(DeskCardStatus.Idle, "DeskIdleBrush")]
    [InlineData(DeskCardStatus.Occupied, "DeskOccupiedBrush")]
    [InlineData(DeskCardStatus.Reserved, "DeskReservedBrush")]
    public void GetStatusStyle_BackgroundKey_MatchesToken(DeskCardStatus status, string expectedBg)
    {
        var (bgKey, _, _) = DeskCard.GetStatusStyle(status);
        Assert.Equal(expectedBg, bgKey);
    }

    [Fact]
    public void GetStatusStyle_Occupied_UsesWhiteText()
    {
        var (_, nameKey, _) = DeskCard.GetStatusStyle(DeskCardStatus.Occupied);
        Assert.Equal("TextOnPrimaryBrush", nameKey); // 蓝底白字，对比度
    }

    [Fact]
    public void GetStatusStyle_Idle_UsesDarkText()
    {
        var (_, nameKey, infoKey) = DeskCard.GetStatusStyle(DeskCardStatus.Idle);
        Assert.Equal("TextPrimaryBrush", nameKey);
        Assert.Equal("TextSecondaryBrush", infoKey);
    }

    [Fact]
    public void RequestMenu_RaisesEvent()
    {
        var card = new DeskCard();
        var fired = false;
        card.MenuRequested += (_, _) => fired = true;
        card.RequestMenu();
        Assert.True(fired);
    }

    [Fact]
    public void OrderItemRow_Subtotal_Normal()
    {
        var row = new OrderItemRow { Quantity = 2, UnitPrice = 25.50m };
        Assert.Equal(51.00m, row.Subtotal);
    }

    [Fact]
    public void OrderItemRow_Subtotal_Gift_IsZero()
    {
        var row = new OrderItemRow { Quantity = 2, UnitPrice = 25.50m, IsGift = true };
        Assert.Equal(0m, row.Subtotal);
        Assert.Equal("赠", OrderItemRow.FormatSubtotal(row.Subtotal, true));
    }

    [Fact]
    public void OrderItemRow_Subtotal_Refunded_IsZero()
    {
        var row = new OrderItemRow { Quantity = 1, UnitPrice = 30m, IsRefunded = true };
        Assert.Equal(0m, row.Subtotal);
    }

    [Fact]
    public void FormatSubtotal_Normal_ShowsYen()
    {
        Assert.Equal("¥51.00", OrderItemRow.FormatSubtotal(51m, false));
    }

    [Fact]
    public void StatusTag_GetStyle_MapsKind()
    {
        Assert.Equal(("SuccessColor", "SuccessBrush"), StatusTag.GetStyle(StatusTagKind.Success));
        Assert.Equal(("DangerColor", "DangerBrush"), StatusTag.GetStyle(StatusTagKind.Danger));
        Assert.Equal(("TextSecondaryColor", "TextSecondaryBrush"), StatusTag.GetStyle(StatusTagKind.Neutral));
    }

    [Fact]
    public void ProductCard_GetStateTag_MapsState()
    {
        Assert.Equal("售罄", ProductCard.GetStateTag(ProductCardState.SoldOut));
        Assert.Equal("称重", ProductCard.GetStateTag(ProductCardState.Weighted));
        Assert.Equal("套餐", ProductCard.GetStateTag(ProductCardState.Package));
        Assert.Equal(string.Empty, ProductCard.GetStateTag(ProductCardState.Normal));
    }
}
