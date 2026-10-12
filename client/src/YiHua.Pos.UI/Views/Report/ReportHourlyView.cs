using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Report;

public partial class ReportHourlyView : UserControl
{
    public ReportHourlyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
