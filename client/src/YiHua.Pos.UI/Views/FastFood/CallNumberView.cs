using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.FastFood;

namespace YiHua.Pos.UI.Views.FastFood;

/// <summary>
/// 叫号取餐页。
/// 当前叫号占最大一块，操作按钮做成大按钮 ——
/// 叫号员一边看客人一边点，按钮小了会点错。
/// </summary>
public partial class CallNumberView : UserControl
{
    public CallNumberView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private CallNumberViewModel? Vm => DataContext as CallNumberViewModel;

    /// <summary>状态栏文案。</summary>
    public event EventHandler<string>? StatusMessage;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Refresh();
    }

    public void Refresh()
    {
        var vm = Vm;
        if (vm == null) return;

        vm.Refresh();

        this.FindControl<TextBlock>("CurrentNumberText")!.Text = vm.CurrentNumber;
        this.FindControl<TextBlock>("CurrentStatusText")!.Text = vm.CurrentStatusText;
        this.FindControl<ItemsControl>("WaitingList")!.ItemsSource = vm.Waiting;
        this.FindControl<ItemsControl>("CalledList")!.ItemsSource = vm.Called;
        this.FindControl<TextBlock>("SummaryText")!.Text =
            $"等待 {vm.WaitingCount} 个 · 已叫未取 {vm.MakingCount} 个 · 今日已发 {vm.IssuedToday} 个";
        this.FindControl<TextBlock>("IssuedText")!.Text = $"今日已发 {vm.IssuedToday} 号";

        var empty = this.FindControl<StackPanel>("EmptyBox");
        if (empty != null) empty.IsVisible = vm.Waiting.Count == 0;

        var message = this.FindControl<TextBlock>("MessageText");
        if (message != null)
            message.Text = vm.ErrorMessage ?? vm.InfoMessage ?? string.Empty;

        var callNext = this.FindControl<Controls.Buttons.PosButton>("CallNextButton");
        if (callNext != null)
            callNext.IsEnabled = vm.WaitingCount > 0;
    }

    private static TicketRowDto? RowOf(object? sender)
        => (sender as Control)?.DataContext as TicketRowDto;

    private void OnIssue(object? sender, RoutedEventArgs e)
    {
        Vm?.Issue();
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? "已发号");
    }

    private void OnCallNext(object? sender, RoutedEventArgs e)
    {
        Vm?.CallNext();
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }

    private void OnCallAgain(object? sender, RoutedEventArgs e)
    {
        Vm?.CallAgain();
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }

    private void OnCallOne(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        Vm?.CallSpecific(row.Number);
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }

    private void OnComplete(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        Vm?.Complete(row.Number);
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }

    private void OnSkip(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        Vm?.Skip(row.Number);
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }

    private void OnPrioritize(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        Vm?.Prioritize(row.Number);
        Refresh();
        StatusMessage?.Invoke(this, Vm?.InfoMessage ?? Vm?.ErrorMessage ?? string.Empty);
    }
}