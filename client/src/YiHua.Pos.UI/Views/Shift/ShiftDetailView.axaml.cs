using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftDetailView : UserControl
{
    public ShiftDetailView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
