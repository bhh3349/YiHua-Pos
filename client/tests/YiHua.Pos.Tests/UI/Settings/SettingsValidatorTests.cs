using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void ValidateStoreName_Empty_Rejected()
    {
        Assert.False(SettingsValidator.ValidateStoreName("", out var error));
        Assert.Contains("不能为空", error);
        Assert.False(SettingsValidator.ValidateStoreName("   ", out _));
        Assert.False(SettingsValidator.ValidateStoreName(null, out _));
    }

    [Fact]
    public void ValidateStoreName_TooLong_Rejected()
    {
        var longName = new string('店', 31);
        Assert.False(SettingsValidator.ValidateStoreName(longName, out var error));
        Assert.Contains("太长", error);
    }

    [Fact]
    public void ValidateStoreName_Valid_Passes()
    {
        Assert.True(SettingsValidator.ValidateStoreName("老王牛肉面", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrinters_Empty_Rejected()
    {
        Assert.False(SettingsValidator.ValidatePrinters(new List<PrinterConfigDto>(), out var error));
        Assert.Contains("打印机", error);
        Assert.False(SettingsValidator.ValidatePrinters(null, out _));
    }

    [Fact]
    public void ValidatePrinters_NoReceiptPrinter_Rejected()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "后厨打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp0" },
        };
        Assert.False(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Contains("小票", error);
    }

    [Fact]
    public void ValidatePrinters_Valid_Passes()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "前台小票机", Usage = PrinterUsage.Receipt, DevicePath = "/dev/usb/lp0" },
            new() { Name = "后厨打印机", Usage = PrinterUsage.Kitchen, DevicePath = "/dev/usb/lp1", Station = "热菜" },
        };
        Assert.True(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrinters_MissingName_Rejected()
    {
        var printers = new List<PrinterConfigDto>
        {
            new() { Name = "", Usage = PrinterUsage.Receipt, DevicePath = "/dev/usb/lp0" },
        };
        Assert.False(SettingsValidator.ValidatePrinters(printers, out var error));
        Assert.Contains("名称", error);
    }

    [Fact]
    public void ValidateServerAddress_Invalid_Rejected()
    {
        Assert.False(SettingsValidator.ValidateServerAddress("", out _));
        Assert.False(SettingsValidator.ValidateServerAddress("not-a-url", out var error));
        Assert.Contains("http", error);
    }

    [Fact]
    public void ValidateServerAddress_Valid_Passes()
    {
        Assert.True(SettingsValidator.ValidateServerAddress("http://192.168.1.100:8000", out _));
        Assert.True(SettingsValidator.ValidateServerAddress("https://api.example.com", out _));
    }
}
