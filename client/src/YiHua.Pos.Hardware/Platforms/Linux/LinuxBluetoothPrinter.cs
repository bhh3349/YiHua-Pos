using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 蓝牙打印机：经 BlueZ 配对并绑定为 RFCOMM 设备（/dev/rfcomm*）后，
/// 与串口设备一样「打开即写字节」。
/// 配对/绑定本身由系统工具（bluetoothctl / rfcomm）完成，不在本类职责内。
/// </summary>
public class LinuxBluetoothPrinter : LinuxPrinterBase
{
    private readonly string _devicePath;

    /// <param name="devicePath">如 /dev/rfcomm0</param>
    /// <param name="streamFactory">设备流工厂（单测注入假实现；默认打开设备文件）</param>
    public LinuxBluetoothPrinter(
        string devicePath = "/dev/rfcomm0",
        Func<string, Stream>? streamFactory = null)
        : base(new FileByteChannel(devicePath, streamFactory ?? OpenDeviceFile))
    {
        _devicePath = devicePath;
    }

    public override string Name => $"蓝牙打印机 {_devicePath}";
    public override PrinterConnection Connection => PrinterConnection.Bluetooth;

    private static Stream OpenDeviceFile(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
}
