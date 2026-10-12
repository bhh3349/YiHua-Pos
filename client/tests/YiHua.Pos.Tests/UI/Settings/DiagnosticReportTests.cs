using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class DiagnosticReportTests
{
    private class NormalProvider : DiagnosticInfoProvider
    {
        public override string AppVersion => "1.2.3";
        public override List<PrinterConfigDto> GetPrinters() => new()
        {
            new() { Name = "前台小票机", DevicePath = "/dev/usb/lp0", Status = PrinterStatus.Online },
            new() { Name = "后厨打印机", DevicePath = "/dev/usb/lp1", Status = PrinterStatus.Offline },
        };
        public override NetworkStatusDto GetNetworkStatus() => new()
        {
            IsConnected = true,
            ServerAddress = "http://192.168.1.100:8000",
            ServerReachable = true,
        };
        public override DatabaseStatusDto GetDatabaseStatus()
        {
            var db = new DatabaseStatusDto
            {
                Exists = true,
                Path = "/data/pos.db",
                SizeMB = 12.5,
                IsHealthy = true,
            };
            db.TableCounts.Add(("orders", 1234));
            db.TableCounts.Add(("products", 56));
            return db;
        }
        public override List<ErrorLogDto> GetRecentErrors(int days) => new()
        {
            new() { Time = DateTime.Now.AddDays(-1), Message = "打印机连接超时" },
        };
    }

    private class BrokenProvider : DiagnosticInfoProvider
    {
        public override List<PrinterConfigDto> GetPrinters() => throw new Exception("打印机服务异常");
        public override NetworkStatusDto GetNetworkStatus() => new()
        {
            IsConnected = false,
            ServerAddress = "http://192.168.1.100:8000",
            ServerReachable = false,
            Error = "网络不可达",
        };
        public override DatabaseStatusDto GetDatabaseStatus() => new() { Exists = false };
        public override List<ErrorLogDto> GetRecentErrors(int days) => throw new Exception("日志文件损坏");
    }

    [Fact]
    public void Build_ContainsAllSections()
    {
        var report = new DiagnosticReportBuilder(new NormalProvider()).Build();
        Assert.Contains("版本信息", report);
        Assert.Contains("1.2.3", report);
        Assert.Contains("打印机状态", report);
        Assert.Contains("前台小票机", report);
        Assert.Contains("在线", report);
        Assert.Contains("离线", report);
        Assert.Contains("网络状态", report);
        Assert.Contains("数据库状态", report);
        Assert.Contains("12.5 MB", report);
        Assert.Contains("错误日志", report);
        Assert.Contains("打印机连接超时", report);
    }

    [Fact]
    public void Build_DatabaseNotExists_DoesNotCrash()
    {
        var report = new DiagnosticReportBuilder(new BrokenProvider()).Build();
        Assert.Contains("未创建", report);
        Assert.Contains("未连接", report); // 离线模式
    }

    [Fact]
    public void Build_PrinterError_DoesNotCrash()
    {
        // 打印机列表获取失败时不崩，继续生成其他部分
        var report = new DiagnosticReportBuilder(new BrokenProvider()).Build();
        Assert.Contains("版本信息", report);
        Assert.Contains("网络状态", report);
    }

    [Fact]
    public void Build_ErrorLogError_DoesNotCrash()
    {
        var report = new DiagnosticReportBuilder(new BrokenProvider()).Build();
        // 错误日志部分不应导致崩溃
        Assert.NotEmpty(report);
    }
}
