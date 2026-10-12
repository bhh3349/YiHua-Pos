using Xunit;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Devices;

namespace YiHua.Pos.Tests.Hardware.Devices;

public class EscPosCashDrawerTests
{
    private sealed class FakePrinter : IReceiptPrinter
    {
        public string Name => "Fake";
        public PrinterConnection Connection => PrinterConnection.Usb;
        public List<byte[]> Printed { get; } = new();
        public bool NativeOpenThrows { get; set; }
        public bool PrintThrows { get; set; }
        public int NativeOpenCalls { get; private set; }

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task PrintAsync(byte[] escposData, CancellationToken ct = default)
        {
            if (PrintThrows) throw new IOException("printer offline");
            Printed.Add((byte[])escposData.Clone());
            return Task.CompletedTask;
        }

        public Task<bool> TestAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new PrinterStatus());

        public Task OpenCashDrawerAsync(CancellationToken ct = default)
        {
            NativeOpenCalls++;
            if (NativeOpenThrows) throw new NotSupportedException("no drawer");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void OpenCommand_IsStandardEscPosSequence()
    {
        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, EscPosCashDrawer.OpenCommand);
    }

    [Fact]
    public async Task OpenAsync_PrefersNativePrinterCapability()
    {
        var printer = new FakePrinter();
        var drawer = new EscPosCashDrawer(printer);

        await drawer.OpenAsync();

        Assert.Equal(1, printer.NativeOpenCalls);
        Assert.Empty(printer.Printed); // 原生成功，不再发原始字节
    }

    [Fact]
    public async Task OpenAsync_FallsBackToRawBytes_WhenNativeUnsupported()
    {
        var printer = new FakePrinter { NativeOpenThrows = true };
        var logs = new List<string>();
        var drawer = new EscPosCashDrawer(printer, logs.Add);

        await drawer.OpenAsync();

        Assert.Single(printer.Printed);
        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, printer.Printed[0]);
        Assert.NotEmpty(logs); // 记录了降级
    }

    [Fact]
    public async Task OpenAsync_DoesNotThrow_WhenEverythingFails()
    {
        var printer = new FakePrinter { NativeOpenThrows = true, PrintThrows = true };
        var logs = new List<string>();
        var drawer = new EscPosCashDrawer(printer, logs.Add);

        // 优雅降级：不抛异常
        await drawer.OpenAsync();

        Assert.NotEmpty(logs);
    }

    [Fact]
    public void Ctor_Throws_OnNullPrinter()
    {
        Assert.Throws<ArgumentNullException>(() => new EscPosCashDrawer(null!));
    }
}
