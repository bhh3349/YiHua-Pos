using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.UI.ViewModels.Settings;

/// <summary>
/// 保存状态。
/// </summary>
public enum SaveState
{
    Saved,      // 已保存
    Dirty,      // 有未保存改动
    Saving,     // 保存中
    Failed,     // 保存失败
}

/// <summary>
/// 设置基类：跟踪保存状态。
/// </summary>
public abstract class SettingsViewModelBase
{
    public SaveState State { get; protected set; } = SaveState.Saved;
    public string? SaveError { get; protected set; }

    protected void MarkDirty()
    {
        if (State == SaveState.Saved)
            State = SaveState.Dirty;
    }

    protected void MarkSaved()
    {
        State = SaveState.Saved;
        SaveError = null;
    }

    protected void MarkSaving() => State = SaveState.Saving;

    protected void MarkFailed(string error)
    {
        State = SaveState.Failed;
        SaveError = error;
    }

    public string StateText => State switch
    {
        SaveState.Saved => "已保存",
        SaveState.Dirty => "有未保存的改动",
        SaveState.Saving => "保存中…",
        SaveState.Failed => $"保存失败：{SaveError}",
        _ => "",
    };
}

/// <summary>
/// 门店信息 ViewModel。
/// </summary>
public class StoreInfoViewModel : SettingsViewModelBase
{
    private readonly ISettingsStore _store;

    public StoreInfoViewModel(ISettingsStore store)
    {
        _store = store;
    }

    public string StoreName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte[]? LogoData { get; set; }

    public string? ValidationError { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var info = await _store.GetStoreInfoAsync(ct);
        StoreName = info.StoreName;
        Address = info.Address;
        Phone = info.Phone;
        LogoData = info.LogoData;
        MarkSaved();
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (!SettingsValidator.ValidateStoreName(StoreName, out var error))
        {
            ValidationError = error;
            return false;
        }
        ValidationError = null;

        MarkSaving();
        try
        {
            await _store.SaveStoreInfoAsync(new StoreInfoDto
            {
                StoreName = StoreName.Trim(),
                Address = Address.Trim(),
                Phone = Phone.Trim(),
                LogoData = LogoData,
            }, ct);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            MarkFailed($"保存失败：{ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// 打印机设置 ViewModel。
/// </summary>
public class PrinterSettingsViewModel : SettingsViewModelBase
{
    private readonly ISettingsStore _store;

    public PrinterSettingsViewModel(ISettingsStore store)
    {
        _store = store;
    }

    public List<PrinterConfigDto> Printers { get; } = new();
    public List<DiscoveredPrinterDto> Discovered { get; private set; } = new();

    public string? ValidationError { get; private set; }
    public string? TestPrintMessage { get; private set; }

    /// <summary>当前状态一句话（"已配置 2 台，其中小票机 1 台"）。</summary>
    public string StateText { get; private set; } = string.Empty;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Printers.Clear();
        Printers.AddRange(await _store.GetPrintersAsync(ct));
        Discovered = PrinterDiscovery.Discover();
        RefreshStateText();
        MarkSaved();
    }

    private void RefreshStateText()
    {
        if (Printers.Count == 0)
        {
            StateText = Discovered.Count == 0
                ? "还没配置打印机。请先在 Windows「设备和打印机」里装好小票机，再回来添加。"
                : $"还没配置打印机。下面发现了 {Discovered.Count} 个可用设备，点「添加」选一台。";
            return;
        }

        var receipt = Printers.Count(p => p.Usage == PrinterUsage.Receipt && p.Enabled);
        var kitchen = Printers.Count(p => p.Usage == PrinterUsage.Kitchen && p.Enabled);
        StateText = $"已配置 {Printers.Count} 台：小票机 {receipt} 台，后厨机 {kitchen} 台。";
        if (receipt == 0)
            StateText += " 还没设小票机，结账后打不出小票。";
    }

    /// <summary>把发现的设备加进配置。</summary>
    public (bool Ok, string? Error) AddPrinter(DiscoveredPrinterDto discovered)
    {
        if (discovered == null) return (false, "没有选中设备");

        if (Printers.Any(p => string.Equals(p.DevicePath, discovered.DevicePath, StringComparison.OrdinalIgnoreCase)))
            return (false, $"「{discovered.DisplayName}」已经在配置里了");

        // 第一台默认当小票机 —— 商家装完第一件事就是要能打小票
        var usage = Printers.Count == 0 ? PrinterUsage.Receipt : PrinterUsage.Kitchen;

        Printers.Add(new PrinterConfigDto
        {
            Name = discovered.DisplayName,
            DevicePath = discovered.DevicePath,
            Usage = usage,
            PaperWidth = 80,
            Enabled = true,
        });

        RefreshStateText();
        MarkDirty();
        return (true, null);
    }

    /// <summary>从配置里移除。</summary>
    public (bool Ok, string? Error) RemovePrinter(PrinterConfigDto printer)
    {
        if (printer == null) return (false, "没有选中打印机");

        var removed = Printers.Remove(printer);
        if (!removed) return (false, "这台打印机已经不在配置里了");

        RefreshStateText();
        MarkDirty();
        return (true, null);
    }

    /// <summary>
    /// 真的打一张测试页出去。
    /// 只生成内容不发送是没意义的 —— 商家点「测试」就是要确认纸能出来。
    /// </summary>
    public async Task<bool> TestPrintAsync(PrinterConfigDto printer, CancellationToken ct = default)
    {
        if (printer == null)
        {
            TestPrintMessage = "没有选中打印机";
            return false;
        }

        if (string.IsNullOrWhiteSpace(printer.DevicePath))
        {
            TestPrintMessage = $"「{printer.Name}」没有设备地址，请删除后重新添加。";
            return false;
        }

        try
        {
            var content = BuildTestPrintContent(printer);
            var bytes = BuildTestPageBytes(content, printer.PaperWidth);

            var target = new YiHua.Pos.Hardware.Platforms.Windows.WindowsRawPrinter(
                printer.DevicePath, printer.Usage == PrinterUsage.Kitchen
                    ? YiHua.Pos.Domain.Enums.PrinterConnection.Network
                    : YiHua.Pos.Domain.Enums.PrinterConnection.Usb);

            if (!await target.ConnectAsync(ct))
            {
                TestPrintMessage = $"系统里找不到打印机「{printer.DevicePath}」。请确认已在 Windows「设备和打印机」里装好。";
                return false;
            }

            await target.PrintAsync(bytes, ct);
            TestPrintMessage = $"测试页已发送到「{printer.Name}」，请查看是否出纸。";
            return true;
        }
        catch (Exception ex)
        {
            TestPrintMessage = $"测试打印失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>把测试页文本编成 ESC/POS 字节（可测试的纯逻辑）。</summary>
    public static byte[] BuildTestPageBytes(string content, int paperWidthMm)
    {
        var builder = new YiHua.Pos.Hardware.Printing.EscPos.EscPosBuilder();
        builder.Init()
               .Align(YiHua.Pos.Hardware.Printing.EscPos.AlignStyle.Left)
               .Charset(YiHua.Pos.Hardware.Printing.EscPos.CharsetType.Gb18030);

        foreach (var line in content.Split('\n'))
            builder.TextLine(line.TrimEnd('\r'));

        builder.Feed(3)
               .Cut(YiHua.Pos.Hardware.Printing.EscPos.CutMode.Full);
        return builder.Build();
    }

    /// <summary>
    /// 测试打印（可测试逻辑部分）。
    /// </summary>
    public string BuildTestPrintContent(PrinterConfigDto printer)
    {
        return $"=== 测试页 ===\n打印机：{printer.Name}\n" +
               $"纸宽：{printer.PaperWidth}mm\n用途：{GetUsageText(printer.Usage)}\n" +
               $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
               $"如果您看到这张纸，说明打印机配置正确！";
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (!SettingsValidator.ValidatePrinters(Printers, out var error))
        {
            ValidationError = error;
            return false;
        }
        ValidationError = null;

        MarkSaving();
        try
        {
            await _store.SavePrintersAsync(Printers.ToList(), ct);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            MarkFailed($"保存失败：{ex.Message}");
            return false;
        }
    }

    private static string GetUsageText(PrinterUsage usage) => usage switch
    {
        PrinterUsage.Receipt => "小票",
        PrinterUsage.Kitchen => "后厨",
        PrinterUsage.Label => "标签",
        PrinterUsage.Shift => "交班单",
        _ => "未知",
    };
}

/// <summary>
/// 收银规则 ViewModel。
/// </summary>
public class CashRuleViewModel : SettingsViewModelBase
{
    private readonly ISettingsStore _store;

    public CashRuleViewModel(ISettingsStore store)
    {
        _store = store;
    }

    public RoundingRule RoundingRule { get; set; } = RoundingRule.None;
    public decimal PreviewAmount { get; set; } = 38.5m;

    /// <summary>抹零预览（实时更新）。</summary>
    public string RoundingPreview => RoundingCalculator.Preview(PreviewAmount, RoundingRule);

    public bool AllowItemDiscount { get; set; } = true;
    public bool AllowOrderDiscount { get; set; } = true;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var rule = await _store.GetCashRuleAsync(ct);
        RoundingRule = rule.RoundingRule;
        AllowItemDiscount = rule.AllowItemDiscount;
        AllowOrderDiscount = rule.AllowOrderDiscount;
        MarkSaved();
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        MarkSaving();
        try
        {
            await _store.SaveCashRuleAsync(new CashRuleDto
            {
                RoundingRule = RoundingRule,
                AllowItemDiscount = AllowItemDiscount,
                AllowOrderDiscount = AllowOrderDiscount,
            }, ct);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            MarkFailed($"保存失败：{ex.Message}");
            return false;
        }
    }
}
