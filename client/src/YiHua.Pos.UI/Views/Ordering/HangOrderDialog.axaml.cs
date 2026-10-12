using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>挂单列表弹窗。</summary>
public partial class HangOrderDialog : Window
{
    public HangOrderDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
