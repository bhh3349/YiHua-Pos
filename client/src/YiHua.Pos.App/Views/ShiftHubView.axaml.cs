using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.Shift;
using YiHua.Pos.UI.ViewModels.Shift;
using YiHua.Pos.UI.Views.Shift;
using PosButton = YiHua.Pos.UI.Controls.Buttons.PosButton;

namespace YiHua.Pos.App.Views;

/// <summary>
/// 交接班页（参考端 C000100061/62、C003220035：交班 / 开钱箱 / 交班记录）。
///
/// 一页管四件事，按有没有进行中的班自动切换：
///   没有班 → 直接显示开班表单（收银员上班第一件事就是开班）
///   有班   → 显示当班统计 + 去交班
///   点交班 → 切到交班对账表单
///   页签   → 交班记录
///
/// 参考端把「去交班」和「开钱箱」放在收银台快捷入口，
/// 这里合并进同一页，少一次跳转。
/// </summary>
public partial class ShiftHubView : UserControl
{
    private readonly IShiftService _shiftService;
    private readonly YiHua.Pos.Domain.Interfaces.IOrderRepository _orders;
    private readonly long _operatorId;
    private readonly string _operatorName;
    private readonly string _terminalNo;

    private Shift? _currentShift;
    private bool _showingHistory;

    public ShiftHubView(
        IShiftService shiftService,
        YiHua.Pos.Domain.Interfaces.IOrderRepository orders,
        long operatorId,
        string operatorName,
        string terminalNo)
    {
        _shiftService = shiftService;
        _orders = orders;
        _operatorId = operatorId;
        _operatorName = operatorName;
        _terminalNo = terminalNo;

        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>状态栏文案（由 App 层显示）。</summary>
    public event EventHandler<string>? StatusMessage;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ = RefreshAsync();
    }

    /// <summary>按当前班次状态决定显示哪一屏。</summary>
    public async Task RefreshAsync()
    {
        _currentShift = await _shiftService.GetCurrentAsync();

        var actionBar = this.FindControl<StackPanel>("ActionBar")!;
        var hint = this.FindControl<TextBlock>("HintText")!;
        var closeBtn = this.FindControl<PosButton>("CloseShiftButton")!;

        if (_showingHistory)
        {
            ShowHistory();
            return;
        }

        actionBar.IsVisible = true;

        if (_currentShift == null)
        {
            // 没有进行中的班：先开班
            closeBtn.IsVisible = false;
            hint.Text = "本班还没开始，先录入备用金开班。";
            ShowOpen();
        }
        else
        {
            closeBtn.IsVisible = true;
            hint.Text = $"{_currentShift.OperatorName} 于 {_currentShift.StartAt:HH:mm} 开班";
            ShowCurrent();
        }

        UpdateTabHighlight();
    }

    private void ShowOpen()
    {
        var view = new ShiftOpenView
        {
            DataContext = new ShiftOpenViewModel(_shiftService),
            OperatorId = _operatorId,
            OperatorName = _operatorName,
            TerminalNo = _terminalNo,
        };
        view.Opened += async (_, _) =>
        {
            StatusMessage?.Invoke(this, "已开班");
            await RefreshAsync();
        };
        this.FindControl<ContentControl>("ContentHost")!.Content = view;
    }

    private void ShowCurrent()
    {
        var view = new ShiftCurrentView
        {
            DataContext = new ShiftCurrentViewModel(_shiftService, _orders),
        };
        this.FindControl<ContentControl>("ContentHost")!.Content = view;
    }

    private void ShowClose()
    {
        if (_currentShift == null) return;

        var view = new ShiftCloseView
        {
            DataContext = new ShiftCloseViewModel(_shiftService, _orders),
            OperatorName = _operatorName,
        };
        view.Cancelled += async (_, _) => await RefreshAsync();
        view.Closed += async (_, _) =>
        {
            StatusMessage?.Invoke(this, "已交班");
            await RefreshAsync();
        };
        this.FindControl<ContentControl>("ContentHost")!.Content = view;
    }

    private void ShowHistory()
    {
        this.FindControl<StackPanel>("ActionBar")!.IsVisible = false;

        var view = new ShiftHistoryView
        {
            DataContext = new ShiftHistoryViewModel(_shiftService),
        };
        this.FindControl<ContentControl>("ContentHost")!.Content = view;
        UpdateTabHighlight();
    }

    private void UpdateTabHighlight()
    {
        var current = this.FindControl<Button>("TabCurrent")!;
        var history = this.FindControl<Button>("TabHistory")!;

        var on = new SolidColorBrush(Color.Parse("#2F6BFF"));
        var onText = new SolidColorBrush(Colors.White);
        var off = new SolidColorBrush(Colors.Transparent);
        var offText = new SolidColorBrush(Color.Parse("#1A2233"));

        current.Background = _showingHistory ? off : on;
        current.Foreground = _showingHistory ? offText : onText;
        history.Background = _showingHistory ? on : off;
        history.Foreground = _showingHistory ? onText : offText;
    }

    private async void OnTabCurrent(object? sender, RoutedEventArgs e)
    {
        _showingHistory = false;
        await RefreshAsync();
    }

    private async void OnTabHistory(object? sender, RoutedEventArgs e)
    {
        _showingHistory = true;
        await RefreshAsync();
    }

    private async void OnCloseShift(object? sender, RoutedEventArgs e)
    {
        if (_currentShift == null)
        {
            StatusMessage?.Invoke(this, "当前没有进行中的班次。");
            return;
        }
        ShowClose();
        await Task.CompletedTask;
    }

    private void OnOpenCashBox(object? sender, RoutedEventArgs e)
    {
        // 钱箱挂在打印机 RJ11 上，走 ESC/POS 开箱指令。
        // 打印机还没接通（见打印任务），这里先明确说明而不是假装成功。
        StatusMessage?.Invoke(this, "开钱箱需要先配好小票打印机（设置 → 打印机管理），当前没有可用打印机。");
    }
}