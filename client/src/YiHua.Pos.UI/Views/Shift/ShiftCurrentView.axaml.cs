using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftCurrentView : UserControl
{
    public ShiftCurrentView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ShiftCurrentViewModel? ViewModel => DataContext as ShiftCurrentViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ViewModel != null)
            _ = ViewModel.LoadAsync();
    }
}
