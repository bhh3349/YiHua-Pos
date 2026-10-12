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

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Printers.Clear();
        Printers.AddRange(await _store.GetPrintersAsync(ct));
        Discovered = PrinterDiscovery.Discover();
        MarkSaved();
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
