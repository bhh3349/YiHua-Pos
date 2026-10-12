using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftHistoryView : UserControl
{
    public ShiftHistoryView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ShiftHistoryViewModel? ViewModel => DataContext as ShiftHistoryViewModel;

    public event EventHandler<ShiftHistoryRow>? ReprintRequested;

    private void OnReprint(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ShiftHistoryRow row)
            ReprintRequested?.Invoke(this, row);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ViewModel != null)
            _ = ViewModel.LoadAsync();
    }
}
