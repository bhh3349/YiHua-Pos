using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Report;

public partial class ReportDetailView : UserControl
{
    public ReportDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
