using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Platforms.Linux;
using Xunit;

namespace YiHua.Pos.Tests.Hardware.Linux;

/// <summary>
/// 设备发现测试：注入假枚举 + 假探测，断言能识别「假打印机」并排除普通串口设备。
/// </summary>
public class PrinterDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_Identifies_Printer_And_Excludes_NonPrinter()
    {
        var discovery = new LinuxPrinterDiscovery(
            serialEnumerator: () => new[] { "/dev/ttyUSB0", "/dev/ttyUSB1" },
            usbEnumerator: () => new[] { "/dev/usb/lp0" },
            bluetoothEnumerator: () => Array.Empty<string>(),
            probe: (dev, ct) => Task.FromResult(dev == "/dev/ttyUSB0")); // 只有 ttyUSB0 有回包

        var result = await discovery.DiscoverAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Address == "/dev/ttyUSB0" && p.Connection == PrinterConnection.Serial);
        Assert.Contains(result, p => p.Address == "/dev/usb/lp0" && p.Connection == PrinterConnection.Usb);
        Assert.DoesNotContain(result, p => p.Address == "/dev/ttyUSB1"); // 无回包的普通串口被排除
    }

    [Fact]
    public async Task DiscoverAsync_Empty_When_No_Devices()
    {
        var discovery = new LinuxPrinterDiscovery(
            serialEnumerator: () => Array.Empty<string>(),
            usbEnumerator: () => Array.Empty<string>(),
            bluetoothEnumerator: () => Array.Empty<string>(),
            probe: (dev, ct) => Task.FromResult(true));

        var result = await discovery.DiscoverAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverAsync_Tolerates_Enumerator_Failure()
    {
        var discovery = new LinuxPrinterDiscovery(
            serialEnumerator: () => throw new UnauthorizedAccessException("no permission"),
            usbEnumerator: () => new[] { "/dev/usb/lp0" },
            bluetoothEnumerator: () => Array.Empty<string>(),
            probe: (dev, ct) => Task.FromResult(false));

        var result = await discovery.DiscoverAsync();

        // 串口枚举炸了也不影响 USB 结果
        Assert.Single(result);
        Assert.Equal("/dev/usb/lp0", result[0].Address);
    }

    [Fact]
    public async Task DiscoverAsync_Respects_Cancellation()
    {
        var discovery = new LinuxPrinterDiscovery(
            serialEnumerator: () => new[] { "/dev/ttyUSB0" },
            usbEnumerator: () => Array.Empty<string>(),
            bluetoothEnumerator: () => Array.Empty<string>(),
            probe: (dev, ct) => Task.FromResult(true));

        using var cts = new CancellationTokenSource();
        var result = await discovery.DiscoverAsync(cts.Token);

        Assert.Single(result);
    }
}
