using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux USB 打印机：走内核 usblp 驱动的 /dev/usb/lp* 设备节点，
/// 打开即写字节，无需 libusb、不需要厂商驱动。
/// </summary>
public class LinuxUsbPrinter : LinuxPrinterBase
{
    private readonly string _devicePath;

    /// <param name="devicePath">如 /dev/usb/lp0</param>
    /// <param name="streamFactory">设备流工厂（单测注入假实现；默认打开设备文件）</param>
    public LinuxUsbPrinter(
        string devicePath = "/dev/usb/lp0",
        Func<string, Stream>? streamFactory = null)
        : base(new FileByteChannel(devicePath, streamFactory ?? OpenDeviceFile))
    {
        _devicePath = devicePath;
    }

    public override string Name => $"USB 打印机 {_devicePath}";
    public override PrinterConnection Connection => PrinterConnection.Usb;

    private static Stream OpenDeviceFile(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
}
