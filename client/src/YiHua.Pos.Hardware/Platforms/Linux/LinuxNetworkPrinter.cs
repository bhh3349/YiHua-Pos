using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 网口打印机：TCP 直连，默认 9100 端口（RAW 打印端口）。
/// </summary>
public class LinuxNetworkPrinter : LinuxPrinterBase
{
    private readonly string _host;
    private readonly int _port;

    /// <param name="host">打印机 IP 或主机名</param>
    /// <param name="port">RAW 端口，默认 9100</param>
    /// <param name="channelFactory">通道工厂（单测注入假实现；默认真实 TCP）</param>
    public LinuxNetworkPrinter(
        string host,
        int port = 9100,
        Func<IByteChannel>? channelFactory = null)
        : base((channelFactory ?? (() => new TcpByteChannel(host, port)))())
    {
        _host = host;
        _port = port;
    }

    public override string Name => $"网络打印机 {_host}:{_port}";
    public override PrinterConnection Connection => PrinterConnection.Network;
}
