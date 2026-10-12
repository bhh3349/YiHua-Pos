using YiHua.Pos.UI.Controls.Buttons;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.Controls.Display;
using YiHua.Pos.UI.Controls.Feedback;
using YiHua.Pos.UI.Controls.Input;
using YiHua.Pos.UI.Controls.Layout;
using Xunit;

namespace YiHua.Pos.Tests.UI;

/// <summary>
/// 所有控件无头实例化冒烟测试：确保构造不抛异常（XAML 加载/资源引用有效）。
/// </summary>
public class ControlSmokeTests
{
    [Fact]
    public void AllControls_CanInstantiate_Headless()
    {
        // Buttons
        _ = new PosButton { Variant = PosButtonVariant.Danger };
        _ = new IconButton { IconText = "×" };
        // Input
        _ = new TouchTextBox();
        // Display
        _ = new StatusTag { Kind = StatusTagKind.Success, Text = "ok" };
        _ = new KeyValueRow { Key = "合计", Value = "128.00", IsAmount = true };
        // Feedback
        _ = new PosToast { Message = "hi" };
        _ = new LoadingOverlay { IsLoading = true };
        _ = new PosModal { Title = "t" };
        _ = new ConfirmDialog { Message = "m" };
        _ = new EmptyState { Title = "t", ActionText = "去添加" };
        // Layout
        _ = new SectionHeader { Title = "s" };
        // Cards/Lists 已在其他测试类中实例化
        _ = new ProductCard();
        _ = new CategoryTab();
    }

    [Fact]
    public void PosButton_Variant_DefaultsToPrimary()
    {
        var btn = new PosButton();
        Assert.Equal(PosButtonVariant.Primary, btn.Variant);
    }

    [Fact]
    public void ConfirmDialog_Confirm_Cancel_RaiseEvents()
    {
        var dlg = new ConfirmDialog();
        var confirmed = false;
        var cancelled = false;
        dlg.Confirmed += (_, _) => confirmed = true;
        dlg.Cancelled += (_, _) => cancelled = true;
        dlg.Confirm();
        dlg.Cancel();
        Assert.True(confirmed);
        Assert.True(cancelled);
    }

    [Fact]
    public void PosModal_RequestClose_RaisesEvent()
    {
        var modal = new PosModal();
        var fired = false;
        modal.CloseRequested += (_, _) => fired = true;
        modal.RequestClose();
        Assert.True(fired);
    }

    [Fact]
    public void EmptyState_InvokeAction_RaisesEvent()
    {
        var state = new EmptyState();
        var fired = false;
        state.ActionClicked += (_, _) => fired = true;
        state.InvokeAction();
        Assert.True(fired);
    }
}
