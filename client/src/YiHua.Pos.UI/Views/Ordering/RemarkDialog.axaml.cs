using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>备注弹窗（最多30字）。</summary>
public partial class RemarkDialog : Window
{
    public RemarkDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
