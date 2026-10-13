using System.Reflection;
using YiHua.Pos.Data;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 诊断信息提供者（真实实现）。
///
/// 这是「一键导出诊断报告」的核心。299 买断没有上门服务，
/// 商家遇到问题只能发微信；这里必须给**真实数据**，
/// 默认虚实现返回的 "1.0.0" / 空列表只能证明代码跑通了，
/// 不能帮我们定位问题。
/// </summary>
internal sealed class AppDiagnosticInfoProvider : DiagnosticInfoProvider
{
    private readonly PosDbContext _ctx;

    public AppDiagnosticInfoProvider(PosDbContext ctx) => _ctx = ctx;

    public override string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public override string DotNetVersion => Environment.Version.ToString();

    public override string OSDescription => Environment.OSVersion.ToString();

    public override string AvaloniaVersion =>
        typeof(Avalonia.Application).Assembly.GetName().Version?.ToString() ?? "未知";

    public override DatabaseStatusDto GetDatabaseStatus()
    {
        var dto = new DatabaseStatusDto
        {
            Path = _ctx.DatabasePath,
            Exists = File.Exists(_ctx.DatabasePath),
        };

        try
        {
            var info = new FileInfo(_ctx.DatabasePath);
            dto.SizeMB = Math.Round(info.Length / 1024.0 / 1024.0, 2);

            // 真实探活：SELECT 1。有表就顺手数几行，出问题时能看出是哪张表空。
            _ctx.Db.Ado.GetInt("SELECT 1");
            dto.IsHealthy = true;
            foreach (var table in new[] { "Product", "ProductCategory", "Desk", "\"Order\"", "Shift" })
            {
                try
                {
                    dto.TableCounts.Add((table, _ctx.Db.Ado.GetInt($"SELECT COUNT(*) FROM {table}")));
                }
                catch (Exception ex)
                {
                    dto.TableCounts.Add((table, -1));
                    dto.IsHealthy = false;
                    dto.HealthError ??= $"表 {table} 查询失败: {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            dto.IsHealthy = false;
            dto.HealthError = ex.Message;
        }

        return dto;
    }
}