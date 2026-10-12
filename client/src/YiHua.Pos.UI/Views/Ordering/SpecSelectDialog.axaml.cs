using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>规格选择弹窗（点选后自动收起）。</summary>
public partial class SpecSelectDialog : Window
{
    public SpecSelectDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
