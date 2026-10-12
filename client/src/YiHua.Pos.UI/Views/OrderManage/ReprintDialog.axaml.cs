using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class ReprintDialog : UserControl
{
    public ReprintDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ReprintViewModel? ViewModel => DataContext as ReprintViewModel;
    public event EventHandler? Cancelled;

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ComboBox cb) return;
        ViewModel.ReprintType = (cb.SelectedItem as ComboBoxItem)?.Content as string ?? "小票";
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    private void OnConfirm(object? s, RoutedEventArgs e) => ViewModel?.Confirm();
}
