using System.IO.Ports;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Platforms.Linux;
using Xunit;

namespace YiHua.Pos.Tests.Hardware.Linux;

/// <summary>串口打印机测试：用假串口断言波特率/数据位/校验位/停止位配置正确。</summary>
public class LinuxSerialPrinterTests
{
    private sealed class FakeSerialPort : ISerialPort
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
        public bool IsOpen { get; private set; }
        public MemoryStream Written { get; } = new();

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void Dispose() { }
    }

    [Fact]
    public async Task ConnectAsync_Configures_SerialPort_Correctly()
    {
        var fake = new FakeSerialPort();
        var printer = new LinuxSerialPrinter("/dev/ttyUSB0", 9600, () => fake);

        await printer.ConnectAsync();

        Assert.Equal("/dev/ttyUSB0", fake.PortName);
        Assert.Equal(9600, fake.BaudRate);
        Assert.Equal(8, fake.DataBits);
        Assert.Equal(Parity.None, fake.Parity);
        Assert.Equal(StopBits.One, fake.StopBits);
        Assert.True(fake.IsOpen);
        Assert.Equal(PrinterConnection.Serial, printer.Connection);
    }

    [Fact]
    public async Task PrintAsync_Writes_Bytes_Through_SerialPort()
    {
        var fake = new FakeSerialPort();
        var printer = new LinuxSerialPrinter("/dev/ttyUSB0", 115200, () => fake);

        var data = new byte[] { 0x1B, 0x40 };
        await printer.PrintAsync(data);

        Assert.Equal(115200, fake.BaudRate);
        Assert.Equal(data, fake.Written.ToArray());
    }

    [Fact]
    public async Task UsbPrinter_Writes_Bytes_To_DeviceStream()
    {
        var mem = new MemoryStream();
        var printer = new LinuxUsbPrinter("/dev/usb/lp0", _ => mem);

        var data = new byte[] { 0x1B, 0x40, 0x0A };
        await printer.PrintAsync(data);

        Assert.Equal(data, mem.ToArray());
        Assert.Equal(PrinterConnection.Usb, printer.Connection);
    }

    [Fact]
    public async Task BluetoothPrinter_Writes_Bytes_To_RfcommStream()
    {
        var mem = new MemoryStream();
        var printer = new LinuxBluetoothPrinter("/dev/rfcomm0", _ => mem);

        var data = new byte[] { 0x1B, 0x40 };
        await printer.PrintAsync(data);

        Assert.Equal(data, mem.ToArray());
        Assert.Equal(PrinterConnection.Bluetooth, printer.Connection);
    }
}
