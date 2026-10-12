using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.SoldOut;

/// <summary>批量沽清</summary>
public partial class SoldOutBatchView : UserControl
{
    public SoldOutBatchView()
    {
        InitializeComponent();
    }

    private void OnSearch(object? sender, RoutedEventArgs e)
        => SearchRequested?.Invoke(this, EventArgs.Empty);

    private void OnConfirm(object? sender, RoutedEventArgs e)
        => Confirmed?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    public event EventHandler? SearchRequested;
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;
}
