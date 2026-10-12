using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
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

public class PrinterSettingsTests
{
    [Fact]
    public void PrinterDiscovery_ReturnsList()
    {
        var printers = PrinterDiscovery.Discover();
        // 至少返回网络打印机占位（各平台都有）
        Assert.NotEmpty(printers);
    }

    [Fact]
    public void IsValidDevicePath_Linux_AcceptsDevPaths()
    {
        if (!OperatingSystem.IsLinux()) return;
        Assert.True(PrinterDiscovery.IsValidDevicePath("/dev/usb/lp0", out _));
        Assert.True(PrinterDiscovery.IsValidDevicePath("tcp://192.168.1.100:9100", out _));
        Assert.False(PrinterDiscovery.IsValidDevicePath("COM1", out var error));
        Assert.Contains("/dev/", error);
    }

    [Fact]
    public void PrinterConfig_MultiPrinterPerOrder()
    {
        // 一张单可配多台打印机
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "前台小票机", Usage = PrinterUsage.Receipt, DevicePath = "/dev/usb/lp0" },
            new() { Name = "热菜打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp1", Station = "热菜" },
            new() { Name = "凉菜打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp2", Station = "凉菜" },
        };
        Assert.True(SettingsValidator.ValidatePrinters(printers, out _));
        Assert.Equal(2, printers.Count(p => p.Usage == PrinterUsage.Kitchen));
    }

    [Fact]
    public void PrinterStatus_TextMapping()
    {
        // 状态用颜色+文字双重表达（View 层用，这里验证枚举完整）
        var statuses = Enum.GetValues<PrinterStatus>();
        Assert.Contains(PrinterStatus.Online, statuses);
        Assert.Contains(PrinterStatus.Offline, statuses);
        Assert.Contains(PrinterStatus.OutOfPaper, statuses);
    }
}

public class NetworkSyncTests
{
    [Fact]
    public void SyncInfo_Offline_ShowsPending()
    {
        var info = new SyncInfoDto
        {
            IsOnline = false,
            PendingCount = 15,
            LastSyncTime = DateTime.Now.AddHours(-2),
            LastError = "网络不可达",
        };
        Assert.False(info.IsOnline);
        Assert.Equal(15, info.PendingCount);
        Assert.NotNull(info.LastSyncTime);
    }

    [Fact]
    public void SyncInfo_Online_NoPending()
    {
        var info = new SyncInfoDto
        {
            IsOnline = true,
            PendingCount = 0,
            LastSyncTime = DateTime.Now,
        };
        Assert.True(info.IsOnline);
        Assert.Equal(0, info.PendingCount);
    }
}
