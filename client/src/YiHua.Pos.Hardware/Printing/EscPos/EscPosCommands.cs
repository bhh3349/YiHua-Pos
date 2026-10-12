namespace YiHua.Pos.Hardware.Printing.EscPos;

/// <summary>
/// ESC/POS 指令常量表（依据爱普生 ESC/POS 公开标准）。
/// 固定指令均为 static readonly，线程安全；带参数的指令由 <see cref="EscPosBuilder"/> 按需构造。
/// </summary>
public static class EscPosCommands
{
    /// <summary>ESC @ —— 初始化打印机</summary>
    public static readonly byte[] Init = { 0x1B, 0x40 };

    /// <summary>FS &amp; —— 进入中文字符模式</summary>
    public static readonly byte[] ChineseModeOn = { 0x1C, 0x26 };

    /// <summary>FS . —— 退出中文字符模式</summary>
    public static readonly byte[] ChineseModeOff = { 0x1C, 0x2E };

    /// <summary>LF —— 打印并换行</summary>
    public static readonly byte[] LineFeed = { 0x0A };

    /// <summary>二维码：选择 Model 2（GS ( k 04 00 31 41 32 00）</summary>
    public static readonly byte[] QrModel2 = { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 };

    /// <summary>二维码：纠错等级 L（GS ( k 03 00 31 45 30）</summary>
    public static readonly byte[] QrErrorCorrectionL = { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30 };

    /// <summary>二维码：打印已存储的符号（GS ( k 03 00 31 51 30）</summary>
    public static readonly byte[] QrPrint = { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 };

    /// <summary>一维码：条码高度 100 点（GS h 100）</summary>
    public static readonly byte[] BarcodeHeight = { 0x1D, 0x68, 0x64 };

    /// <summary>一维码：模块宽度 3（GS w 3）</summary>
    public static readonly byte[] BarcodeWidth = { 0x1D, 0x77, 0x03 };

    /// <summary>一维码：HRI 字符打印在条码下方（GS H 2）</summary>
    public static readonly byte[] BarcodeHriBelow = { 0x1D, 0x48, 0x02 };
}
