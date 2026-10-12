using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Report;

public partial class ReportExportView : UserControl
{
    public ReportExportView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
