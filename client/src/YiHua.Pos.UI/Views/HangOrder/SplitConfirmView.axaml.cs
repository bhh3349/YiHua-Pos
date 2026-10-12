using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.HangOrder;

/// <summary>拆单确认</summary>
public partial class SplitConfirmView : UserControl
{
    public SplitConfirmView()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
        => Confirmed?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;
}
