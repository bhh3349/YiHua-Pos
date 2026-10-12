using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>正餐点单主界面（左商品区 + 右明细区）。</summary>
public partial class OrderEntryView : UserControl
{
    public OrderEntryView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
