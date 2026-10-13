using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.Settings;
using YiHua.Pos.UI.ViewModels.Settings;

namespace YiHua.Pos.UI.Views.Settings;

/// <summary>
/// 打印机管理页。
///
/// 关键点：**「测试」必须真的出纸**。
/// 商家点测试就是为了确认这台机器能打，只弹一句「测试页已生成」是骗人的。
/// </summary>
public partial class PrinterSettingsView : UserControl
{
    public PrinterSettingsView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private PrinterSettingsViewModel? Vm => DataContext as PrinterSettingsViewModel;

    /// <summary>配置变更（由 App 层提示保存）。</summary>
    public event EventHandler<string>? StatusMessage;

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

        this.FindControl<ItemsControl>("ConfiguredList")!.ItemsSource = vm.Printers;
        this.FindControl<ItemsControl>("DiscoveredList")!.ItemsSource = vm.Discovered;
        this.FindControl<TextBlock>("StateText")!.Text = vm.StateText;
        this.FindControl<TextBlock>("TestMessage")!.Text = vm.TestPrintMessage ?? string.Empty;
    }

    // ---------- 已配置列表 ----------

    private async void OnTestConfigured(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not Control c || c.DataContext is not PrinterConfigDto printer) return;

        var ok = await Vm.TestPrintAsync(printer);
        Refresh();
        StatusMessage?.Invoke(this, Vm.TestPrintMessage ?? (ok ? "测试打印完成" : "测试打印失败"));
    }

    private void OnRemoveConfigured(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not Control c || c.DataContext is not PrinterConfigDto printer) return;

        var (ok, error) = Vm.RemovePrinter(printer);
        Refresh();
        StatusMessage?.Invoke(this, ok ? $"已移除「{printer.Name}」，记得点保存。" : error ?? "移除失败");
    }

    // ---------- 发现列表 ----------

    private void OnAddDiscovered(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not Control c || c.DataContext is not DiscoveredPrinterDto discovered) return;

        var (ok, error) = Vm.AddPrinter(discovered);
        Refresh();
        StatusMessage?.Invoke(this, ok ? $"已添加「{discovered.DisplayName}」，记得点保存。" : error ?? "添加失败");
    }

    // ---------- 全局测试打印 ----------

    private async void OnTestPrint(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        // 优先测小票机；没有小票机就测第一台
        var target = vm.Printers.FirstOrDefault(p => p.Usage == PrinterUsage.Receipt && p.Enabled)
                     ?? vm.Printers.FirstOrDefault();

        if (target == null)
        {
            StatusMessage?.Invoke(this, "还没有配置打印机。请先从下面「发现的设备」里添加一台。");
            return;
        }

        await vm.TestPrintAsync(target);
        Refresh();
        StatusMessage?.Invoke(this, vm.TestPrintMessage ?? "测试打印完成");
    }

    // ---------- 保存 ----------

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        var ok = await vm.SaveAsync();
        Refresh();
        StatusMessage?.Invoke(this, ok ? "打印机配置已保存" : vm.ValidationError ?? "保存失败");
    }
}