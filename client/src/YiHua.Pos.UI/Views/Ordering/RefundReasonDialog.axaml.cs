using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>退菜原因弹窗。</summary>
public partial class RefundReasonDialog : Window
{
    public RefundReasonDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
