using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.SoldOut;

/// <summary>定时设置</summary>
public partial class SoldOutScheduleView : UserControl
{
    public SoldOutScheduleView()
    {
        InitializeComponent();
    }

    private void OnSave(object? sender, RoutedEventArgs e)
        => Saved?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Saved;
}
