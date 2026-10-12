using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>配送信息对话框</summary>
public partial class DeliveryDialog : UserControl
{
    public DeliveryDialog()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
        => Confirmed?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Confirmed;
}
