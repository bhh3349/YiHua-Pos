using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Login;

namespace YiHua.Pos.UI.Views.Login;

public partial class StoreSelectView : UserControl
{
    public StoreSelectView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
