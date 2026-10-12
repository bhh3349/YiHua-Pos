using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Devices;

/// <summary>
/// 钱箱（接在打印机的 RJ11 口上）。
/// 「开钱箱」= 往打印机发 ESC/POS 开箱指令。
/// 策略：优先用打印机的原生开箱能力；失败则直接发原始指令字节；
/// 都失败则记日志并优雅降级（不抛异常，不影响收银流程）。
/// </summary>
public sealed class EscPosCashDrawer : ICashDrawer
{
    /// <summary>标准开箱指令：ESC p m t1 t2（m=0 引脚2，t1=25，t2=250）</summary>
    public static readonly byte[] OpenCommand = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

    private readonly IReceiptPrinter _printer;
    private readonly Action<string>? _log;

    public EscPosCashDrawer(IReceiptPrinter printer, Action<string>? log = null)
    {
        _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        _log = log;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        try
        {
            await _printer.OpenCashDrawerAsync(ct).ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[CashDrawer] 打印机原生开箱失败({ex.GetType().Name})，改发 ESC/POS 原始指令");
        }

        try
        {
            var cmd = new byte[OpenCommand.Length];
            Array.Copy(OpenCommand, cmd, OpenCommand.Length);
            await _printer.PrintAsync(cmd, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 优雅降级：记日志，不抛异常
            _log?.Invoke($"[CashDrawer] 开箱指令发送失败，已跳过: {ex.Message}");
        }
    }
}
