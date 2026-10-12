using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskToolbar : UserControl
{
    public DeskToolbar()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private DeskMapViewModel? ViewModel => DataContext as DeskMapViewModel;

    /// <summary>请求开台（由容器处理，打开开台对话框）</summary>
    public event EventHandler? OpenRequested;

    private void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ComboBox cb)
            return;
        var tag = (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        ViewModel.StatusFilter = tag switch
        {
            "Idle" => DeskStatus.Idle,
            "Occupied" => DeskStatus.Occupied,
            "Reserved" => DeskStatus.Reserved,
            _ => null,
        };
    }

    private void OnToggleSort(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;
        ViewModel.IdleFirst = !ViewModel.IdleFirst;
        if (this.FindControl<Button>("SortButton") is { } btn)
            btn.Content = ViewModel.IdleFirst ? "空闲在前" : "占用在前";
    }

    private void OnOpenDesk(object? sender, RoutedEventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);
}
