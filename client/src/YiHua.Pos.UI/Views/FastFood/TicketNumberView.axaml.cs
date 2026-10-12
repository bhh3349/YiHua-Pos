using Avalonia.Controls;
using Avalonia.Interactivity;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>取号条（叫号/发号/叫号屏入口）</summary>
public partial class TicketNumberView : UserControl
{
    public TicketNumberView()
    {
        InitializeComponent();
    }

    public event EventHandler? CallNextRequested;
    public event EventHandler? IssueRequested;
    public event EventHandler? ShowCallBoardRequested;

    private void OnCallNext(object? sender, RoutedEventArgs e)
        => CallNextRequested?.Invoke(this, EventArgs.Empty);

    private void OnIssue(object? sender, RoutedEventArgs e)
        => IssueRequested?.Invoke(this, EventArgs.Empty);

    private void OnShowCallBoard(object? sender, RoutedEventArgs e)
        => ShowCallBoardRequested?.Invoke(this, EventArgs.Empty);
}
