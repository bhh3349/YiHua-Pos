using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Platforms.Linux;
using Xunit;

namespace YiHua.Pos.Tests.Hardware.Linux;

/// <summary>网口打印机测试：用假通道断言「发出去的字节序列」，不依赖真实硬件。</summary>
public class LinuxNetworkPrinterTests
{
    private sealed class FakeByteChannel : IByteChannel
    {
        public MemoryStream Written { get; } = new();
        public byte[] ReadResponse { get; set; } = Array.Empty<byte>();
        public bool IsConnected { get; set; }
        public int ConnectCount { get; private set; }

        public Task ConnectAsync(CancellationToken ct)
        {
            ConnectCount++;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(byte[] data, CancellationToken ct)
        {
            Written.Write(data, 0, data.Length);
            return Task.CompletedTask;
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken ct)
        {
            int n = Math.Min(buffer.Length, ReadResponse.Length);
            Array.Copy(ReadResponse, 0, buffer, 0, n);
            return Task.FromResult(n);
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task PrintAsync_Writes_Exact_Bytes_To_Channel()
    {
        var fake = new FakeByteChannel();
        var printer = new LinuxNetworkPrinter("192.168.1.100", 9100, () => fake);

        var data = new byte[] { 0x1B, 0x40, 0x1B, 0x61, 0x01 };
        await printer.PrintAsync(data);

        Assert.Equal(data, fake.Written.ToArray());
        Assert.Equal(PrinterConnection.Network, printer.Connection);
    }

    [Fact]
    public async Task PrintAsync_AutoReconnects_When_Disconnected()
    {
        var fake = new FakeByteChannel { IsConnected = false };
        var printer = new LinuxNetworkPrinter("192.168.1.100", 9100, () => fake);

        await printer.PrintAsync(new byte[] { 0x1B, 0x40 });
        Assert.Equal(1, fake.ConnectCount); // 第一次打印时自动连接

        fake.IsConnected = false; // 模拟断线
        await printer.PrintAsync(new byte[] { 0x1B, 0x40 });
        Assert.Equal(2, fake.ConnectCount); // 再次自动重连
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x40 }, fake.Written.ToArray());
    }

    [Fact]
    public async Task OpenCashDrawerAsync_Sends_Esc_p_Command()
    {
        var fake = new FakeByteChannel();
        var printer = new LinuxNetworkPrinter("192.168.1.100", 9100, () => fake);

        await printer.OpenCashDrawerAsync();

        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, fake.Written.ToArray());
    }

    [Fact]
    public async Task GetStatusAsync_Parses_Online_Bit()
    {
        var fake = new FakeByteChannel();
        var printer = new LinuxNetworkPrinter("192.168.1.100", 9100, () => fake);
        await printer.ConnectAsync();

        // 回包 Bit3=0 → 在线；同时断言发出了 DLE EOT 1
        fake.ReadResponse = new byte[] { 0x12 };
        var status = await printer.GetStatusAsync();

        Assert.True(status.IsConnected);
        Assert.True(status.IsOnline);
        byte[] sent = fake.Written.ToArray();
        Assert.Equal(new byte[] { 0x10, 0x04, 0x01 }, sent);

        // 回包 Bit3=1 → 离线
        fake.Written.SetLength(0);
        fake.ReadResponse = new byte[] { 0x1A }; // 0x12 | 0x08
        status = await printer.GetStatusAsync();
        Assert.False(status.IsOnline);
    }

    [Fact]
    public async Task GetStatusAsync_NotConnected_Returns_Disconnected()
    {
        var fake = new FakeByteChannel { IsConnected = false };
        var printer = new LinuxNetworkPrinter("192.168.1.100", 9100, () => fake);

        var status = await printer.GetStatusAsync();

        Assert.False(status.IsConnected);
        Assert.Equal(0, fake.Written.Length); // 未连接时不发查询指令
    }
}
