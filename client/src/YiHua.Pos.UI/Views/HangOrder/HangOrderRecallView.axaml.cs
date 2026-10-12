using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.HangOrder;

/// <summary>取单（冲突提示/换桌）</summary>
public partial class HangOrderRecallView : UserControl
{
    public HangOrderRecallView()
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
