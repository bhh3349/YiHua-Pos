using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Member;

namespace YiHua.Pos.UI.Views.Member;

/// <summary>
/// 会员列表页。
/// 每行直接给「充值/调整/流水/编辑/注销」，不用先点进去再找操作 ——
/// 收银台旁边办的会员业务都是急的。
/// </summary>
public partial class MemberListView : UserControl
{
    public MemberListView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MemberListViewModel? Vm => DataContext as MemberListViewModel;

    /// <summary>状态栏文案。</summary>
    public event EventHandler<string>? StatusMessage;

    public event EventHandler? NewMemberRequested;
    public event EventHandler<MemberRowDto>? EditRequested;
    public event EventHandler<MemberRowDto>? RechargeRequested;
    public event EventHandler<MemberRowDto>? AdjustRequested;
    public event EventHandler<MemberRowDto>? TransactionsRequested;
    public event EventHandler<MemberRowDto>? CancelRequested;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var vm = Vm;
        if (vm == null) return;

        await vm.LoadAsync();
        Refresh();
    }

    private void Refresh()
    {
        var vm = Vm;
        if (vm == null) return;

        this.FindControl<ItemsControl>("MemberList")!.ItemsSource = vm.Members;
        this.FindControl<TextBlock>("SummaryText")!.Text =
            $"共 {vm.Members.Count} 张卡（正常 {vm.ActiveCount}），余额合计 ¥{vm.TotalBalance:F2}";
        this.FindControl<TextBlock>("ErrorText")!.Text = vm.ErrorMessage ?? string.Empty;

        var empty = this.FindControl<StackPanel>("EmptyBox");
        if (empty != null) empty.IsVisible = vm.IsEmpty;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vm == null) return;
        Vm.Keyword = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
    }

    private async void OnSearch(object? sender, RoutedEventArgs e) => await LoadAsync();

    private void OnNew(object? sender, RoutedEventArgs e) => NewMemberRequested?.Invoke(this, EventArgs.Empty);

    private static MemberRowDto? RowOf(object? sender)
        => (sender as Control)?.DataContext as MemberRowDto;

    private void OnEdit(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) EditRequested?.Invoke(this, row);
    }

    private void OnRecharge(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) RechargeRequested?.Invoke(this, row);
    }

    private void OnAdjust(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) AdjustRequested?.Invoke(this, row);
    }

    private void OnTransactions(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) TransactionsRequested?.Invoke(this, row);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) CancelRequested?.Invoke(this, row);
    }
}