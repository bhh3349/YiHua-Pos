using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YiHua.Pos.UI.Views.SoldOut;

/// <summary>沽清列表（按档口分组）</summary>
public partial class SoldOutListView : UserControl
{
    public SoldOutListView()
    {
        InitializeComponent();
    }

    private void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long productId)
            RestoreRequested?.Invoke(this, productId);
    }

    private void OnRestoreAll(object? sender, RoutedEventArgs e)
        => RestoreAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnBatch(object? sender, RoutedEventArgs e)
        => BatchRequested?.Invoke(this, EventArgs.Empty);

    private void OnSchedule(object? sender, RoutedEventArgs e)
        => ScheduleRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler<long>? RestoreRequested;
    public event EventHandler? RestoreAllRequested;
    public event EventHandler? BatchRequested;
    public event EventHandler? ScheduleRequested;
}
