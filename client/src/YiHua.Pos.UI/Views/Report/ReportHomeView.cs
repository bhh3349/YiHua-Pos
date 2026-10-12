using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Report;

public partial class ReportHomeView : UserControl
{
    public ReportHomeView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
