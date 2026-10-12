using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

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
