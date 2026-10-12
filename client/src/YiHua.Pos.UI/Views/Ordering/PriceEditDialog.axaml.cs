using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>改价弹窗（仅店长，显示原值+二次确认）。</summary>
public partial class PriceEditDialog : Window
{
    public PriceEditDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
