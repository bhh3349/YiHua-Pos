using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Controls.Cards;
using YiHua.Pos.UI.ViewModels.Desk;
using DDesk = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.Views.Desk;

/// <summary>
/// 桌台地图：加载桌台、把点击转成业务事件。
/// 业务编排（开台窗体、跳转点单）交给 App 层，这里只负责「点到了哪张桌」。
/// </summary>
public partial class DeskMapView : UserControl
{
    private DispatcherTimer? _refreshTimer;

    public DeskMapView() => InitializeComponent();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // 路由事件：卡片是异步生成的，父级订阅一次就够，不会漏挂
        AddHandler(DeskCard.CardClickEvent, OnDeskCardClick);

        if (this.FindControl<DeskToolbar>("Toolbar") is { } toolbar)
            toolbar.OpenRequested += async (_, _) => await RequestOpenDeskAsync();
    }

    private DeskMapViewModel? ViewModel => DataContext as DeskMapViewModel;

    public event EventHandler<DDesk>? DeskOpenRequested;
    public event EventHandler<DDesk>? OccupiedDeskSelected;
    public event EventHandler<string>? StatusMessage;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 必须用 DispatcherTimer（UI 线程），不能用 System.Threading.Timer。
        // RefreshElapsed 会改绑定到界面的 DeskCardModel.ElapsedText，
        // 从线程池线程改它会抛异常，而且是在定时器回调里抛、没有 try/catch 兜底 ——
        // 结果是开着桌台页 30 秒后整个程序崩掉。
        _refreshTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick -= OnRefreshTick;
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();

        if (ViewModel != null)
            _ = ViewModel.LoadAsync();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _refreshTimer?.Stop();
    }

    private void OnRefreshTick(object? sender, EventArgs e) => ViewModel?.RefreshElapsed();

    private async void OnDeskCardClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not DeskCard card || card.DataContext is not DeskCardModel model)
            return;
        if (ViewModel == null)
            return;

        var desk = await ViewModel.GetDeskAsync(model.Id);
        if (desk == null)
        {
            StatusMessage?.Invoke(this, "桌台已不存在，请刷新桌台列表。");
            return;
        }

        switch (desk.Status)
        {
            case DeskStatus.Idle:
                DeskOpenRequested?.Invoke(this, desk);
                break;
            case DeskStatus.Occupied:
                OccupiedDeskSelected?.Invoke(this, desk);
                break;
            default:
                StatusMessage?.Invoke(this, $"{desk.Name} 已预订。");
                break;
        }
    }

    private async Task RequestOpenDeskAsync()
    {
        if (ViewModel == null) return;
        var desk = await ViewModel.GetFirstIdleDeskAsync();
        if (desk == null)
            StatusMessage?.Invoke(this, "没有空闲桌台，请先配置桌台。");
        else
            DeskOpenRequested?.Invoke(this, desk);
    }

    /// <summary>空状态里的「+ 开台」按钮。</summary>
    private async void OnOpenRequested(object? sender, RoutedEventArgs e) => await RequestOpenDeskAsync();
}