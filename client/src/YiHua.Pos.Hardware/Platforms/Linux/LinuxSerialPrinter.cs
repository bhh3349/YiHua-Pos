using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 串口打印机（/dev/ttyUSB*、/dev/ttyACM*、/dev/ttyS*）。
/// 用 BCL 的 SerialPort（System.IO.Ports），默认 9600-8-N-1。
/// </summary>
public class LinuxSerialPrinter : LinuxPrinterBase
{
    private readonly string _portName;

    /// <param name="portName">如 /dev/ttyUSB0</param>
    /// <param name="baudRate">波特率，默认 9600</param>
    /// <param name="portFactory">串口工厂（单测注入假实现；默认真实串口）</param>
    public LinuxSerialPrinter(
        string portName,
        int baudRate = 9600,
        Func<ISerialPort>? portFactory = null)
        : base(new SerialByteChannel((portFactory ?? (() => new SerialPortAdapter()))(), portName, baudRate))
    {
        _portName = portName;
    }

    public override string Name => $"串口打印机 {_portName}";
    public override PrinterConnection Connection => PrinterConnection.Serial;
}
