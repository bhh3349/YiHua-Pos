using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>下单结果视图（成功/失败/重试/冲突）。</summary>
public partial class SubmitResultView : UserControl
{
    public SubmitResultView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
