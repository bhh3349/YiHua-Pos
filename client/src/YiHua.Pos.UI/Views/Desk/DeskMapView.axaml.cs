using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Desk;

namespace YiHua.Pos.UI.Views.Desk;

public partial class DeskMapView : UserControl
{
    private System.Threading.Timer? _refreshTimer;

    public DeskMapView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        if (this.FindControl<DeskToolbar>("Toolbar") is { } toolbar)
            toolbar.OpenRequested += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private DeskMapViewModel? ViewModel => DataContext as DeskMapViewModel;

    /// <summary>请求开台（由 App 层打开对话框）</summary>
    public event EventHandler? OpenRequested;

    /// <summary>桌台被点击（打开操作菜单）</summary>
    public event EventHandler<DeskCardModel>? DeskClicked;

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // 每 30 秒刷新占用时长
        _refreshTimer = new System.Threading.Timer(_ => ViewModel?.RefreshElapsed(),
            null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        if (ViewModel != null)
            _ = ViewModel.LoadAsync();
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    private void OnOpenRequested(object? sender, RoutedEventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);
}
