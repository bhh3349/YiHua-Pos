using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 配置校验（纯逻辑）。
/// 错误信息说人话，不抛技术术语。
/// </summary>
public static class SettingsValidator
{
    /// <summary>
    /// 店名必填（要打到小票上）。
    /// </summary>
    public static bool ValidateStoreName(string? name, out string? error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "店名不能为空，小票上要打印店名，客人看不到店名会觉得不正规";
            return false;
        }
        if (name.Trim().Length > 30)
        {
            error = "店名太长了（最多 30 个字），小票纸宽印不下";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 打印机配置校验：至少要有 1 台能打小票的。
    /// </summary>
    public static bool ValidatePrinters(IReadOnlyList<PrinterConfigDto>? printers, out string? error)
    {
        if (printers == null || printers.Count == 0)
        {
            error = "还没有配置打印机，结账后打不出小票。请先添加一台打印机。";
            return false;
        }
        var receiptPrinters = printers.Where(p => p.Enabled && p.Usage == PrinterUsage.Receipt).ToList();
        if (receiptPrinters.Count == 0)
        {
            error = "没有可用的小票打印机。请至少启用一台用途为「小票」的打印机。";
            return false;
        }
        foreach (var p in printers)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                error = "有台打印机没填名称，填个名字方便区分（比如「前台小票机」）。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(p.DevicePath))
            {
                error = $"打印机「{p.Name}」没填设备路径，不知道连在哪台设备上。";
                return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 服务器地址校验。
    /// </summary>
    public static bool ValidateServerAddress(string? address, out string? error)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            error = "服务器地址不能为空。单机使用可以填 http://localhost:8000";
            return false;
        }
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            error = "服务器地址格式不对，要以 http:// 或 https:// 开头，比如 http://192.168.1.100:8000";
            return false;
        }
        error = null;
        return true;
    }
}

/// <summary>
/// 抹零计算（纯逻辑）。
/// </summary>
public static class RoundingCalculator
{
    /// <summary>
    /// 应用抹零规则。**向下抹掉**，不是四舍五入。
    ///
    /// 抹到角：¥38.54 → ¥38.50
    /// 抹到元：¥38.50 → ¥38.00
    ///
    /// ⚠️ 必须用 Floor。用 Math.Round 会变成「往上凑整」——
    /// ¥38.54 抹到角变 ¥38.60，等于多收客人 6 分钱。
    /// 这是收银口径问题，不是精度问题。
    /// </summary>
    public static decimal ApplyRounding(decimal amount, RoundingRule rule)
    {
        if (amount <= 0m)
            return 0m;

        return rule switch
        {
            RoundingRule.None => Round(amount),
            RoundingRule.ToJiao => Round(Math.Floor(amount * 10m) / 10m),
            RoundingRule.ToYuan => Round(Math.Floor(amount)),
            _ => Round(amount),
        };
    }

    /// <summary>
    /// 预览文本：输入 ¥38.5，按规则显示「抹到元 → ¥38.0」。
    /// </summary>
    public static string Preview(decimal amount, RoundingRule rule)
    {
        var result = ApplyRounding(amount, rule);
        var ruleName = rule switch
        {
            RoundingRule.None => "不抹零",
            RoundingRule.ToJiao => "抹到角",
            RoundingRule.ToYuan => "抹到元",
            _ => "未知",
        };
        return $"{ruleName} → ¥{result:F1}";
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
