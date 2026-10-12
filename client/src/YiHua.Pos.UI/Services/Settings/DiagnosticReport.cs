using System.Text;

namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// 诊断报告构建器（纯逻辑）。
/// 输出 Markdown，商家发给客服，30 秒看懂。
/// 数据库不存在/网络断开时不能崩。
/// </summary>
public class DiagnosticReportBuilder
{
    private readonly DiagnosticInfoProvider _provider;

    public DiagnosticReportBuilder(DiagnosticInfoProvider provider)
    {
        _provider = provider;
    }

    public string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 逸华收银 诊断报告");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 1. 版本信息
        sb.AppendLine("## 版本信息");
        sb.AppendLine($"- 软件版本：{_provider.AppVersion}");
        sb.AppendLine($"- .NET 版本：{_provider.DotNetVersion}");
        sb.AppendLine($"- 操作系统：{_provider.OSDescription}");
        sb.AppendLine($"- Avalonia 版本：{_provider.AvaloniaVersion}");
        sb.AppendLine();

        // 2. 打印机状态
        sb.AppendLine("## 打印机状态");
        var printers = SafeGet(() => _provider.GetPrinters(), "获取打印机列表失败");
        if (printers.Count == 0)
        {
            sb.AppendLine("- 未配置打印机");
        }
        else
        {
            foreach (var p in printers)
                sb.AppendLine($"- {p.Name}（{p.DevicePath}）：{GetStatusText(p.Status)}");
        }
        sb.AppendLine();

        // 3. 网络状态
        sb.AppendLine("## 网络状态");
        var network = SafeGet(() => _provider.GetNetworkStatus(), "获取网络状态失败");
        sb.AppendLine($"- 网络：{(network.IsConnected ? "已连接" : "未连接（离线模式）")}");
        sb.AppendLine($"- 服务器：{network.ServerAddress}");
        sb.AppendLine($"- 服务器连接：{(network.ServerReachable ? "可达" : "不可达")}");
        if (!string.IsNullOrEmpty(network.Error))
            sb.AppendLine($"- 错误：{network.Error}");
        sb.AppendLine();

        // 4. 数据库状态
        sb.AppendLine("## 数据库状态");
        var db = SafeGet(() => _provider.GetDatabaseStatus(), "获取数据库状态失败");
        if (!db.Exists)
        {
            sb.AppendLine("- 数据库：未创建（首次启动会自动创建）");
        }
        else
        {
            sb.AppendLine($"- 路径：{db.Path}");
            sb.AppendLine($"- 大小：{db.SizeMB:F1} MB");
            sb.AppendLine($"- 完整性：{(db.IsHealthy ? "正常" : "异常，" + db.HealthError)}");
            foreach (var (table, count) in db.TableCounts)
                sb.AppendLine($"- {table}：{count} 条");
        }
        sb.AppendLine();

        // 5. 最近 7 天错误日志
        sb.AppendLine("## 最近 7 天错误日志");
        var errors = SafeGet(() => _provider.GetRecentErrors(7), "获取错误日志失败");
        if (errors.Count == 0)
        {
            sb.AppendLine("- 无错误记录");
        }
        else
        {
            foreach (var e in errors.Take(50)) // 最多 50 条
                sb.AppendLine($"- [{e.Time:MM-dd HH:mm}] {e.Message}");
        }

        return sb.ToString();
    }

    private static List<T> SafeGet<T>(Func<List<T>> getter, string errorMsg)
    {
        try { return getter(); }
        catch { return new List<T>(); }
    }

    private static T SafeGet<T>(Func<T> getter, string errorMsg) where T : new()
    {
        try { return getter(); }
        catch { return new T(); }
    }

    private static string GetStatusText(PrinterStatus status) => status switch
    {
        PrinterStatus.Online => "在线",
        PrinterStatus.Offline => "离线",
        PrinterStatus.OutOfPaper => "缺纸",
        PrinterStatus.Error => "故障",
        _ => "未知",
    };
}

/// <summary>
/// 诊断信息提供者（App 层实现）。
/// </summary>
public abstract class DiagnosticInfoProvider
{
    public virtual string AppVersion => "1.0.0";
    public virtual string DotNetVersion => Environment.Version.ToString();
    public virtual string OSDescription => Environment.OSVersion.ToString();
    public virtual string AvaloniaVersion => "11.x";

    public virtual List<PrinterConfigDto> GetPrinters() => new();
    public virtual NetworkStatusDto GetNetworkStatus() => new();
    public virtual DatabaseStatusDto GetDatabaseStatus() => new();
    public virtual List<ErrorLogDto> GetRecentErrors(int days) => new();
}

public class NetworkStatusDto
{
    public bool IsConnected { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public bool ServerReachable { get; set; }
    public string? Error { get; set; }
}

public class DatabaseStatusDto
{
    public bool Exists { get; set; }
    public string Path { get; set; } = string.Empty;
    public double SizeMB { get; set; }
    public bool IsHealthy { get; set; } = true;
    public string? HealthError { get; set; }
    public List<(string Table, long Count)> TableCounts { get; } = new();
}

public class ErrorLogDto
{
    public DateTime Time { get; set; }
    public string Message { get; set; } = string.Empty;
}
