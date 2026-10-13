using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskOpenDialog : Window
{
    public DeskOpenDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private DeskOpenViewModel? ViewModel => DataContext as DeskOpenViewModel;
    public event EventHandler? OpenedSuccessfully;

    private void OnCancel(object? s, RoutedEventArgs e) => Close();

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null && await ViewModel.ConfirmAsync())
        {
            OpenedSuccessfully?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }
}