using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YiHua.Pos.UI.Services.HangOrder;

namespace YiHua.Pos.UI.Views.HangOrder;

/// <summary>挂单列表（取单入口）</summary>
public partial class HangOrderView : UserControl
{
    public HangOrderView()
    {
        InitializeComponent();
    }

    private void OnRecordClick(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Control c && c.Tag is HangOrderRecord r)
            DetailRequested?.Invoke(this, r);
        else if (e.ClickCount == 1 && sender is Control c1 && c1.Tag is HangOrderRecord r1)
            RecallRequested?.Invoke(this, r1);
    }

    private void OnRecall(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HangOrderRecord r)
            RecallRequested?.Invoke(this, r);
    }

    private void OnClean(object? sender, RoutedEventArgs e)
        => CleanRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler<HangOrderRecord>? RecallRequested;
    public event EventHandler<HangOrderRecord>? DetailRequested;
    public event EventHandler? CleanRequested;
}
