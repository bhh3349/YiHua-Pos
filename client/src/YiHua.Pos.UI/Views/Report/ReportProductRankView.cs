using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Report;

public partial class ReportProductRankView : UserControl
{
    public ReportProductRankView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
