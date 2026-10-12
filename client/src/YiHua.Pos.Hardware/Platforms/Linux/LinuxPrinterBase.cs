using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Printing.EscPos;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 打印机基类：实现 <see cref="IReceiptPrinter"/> 的通用逻辑，
/// 具体通路（串口/USB/网口/蓝牙）只提供不同的 <see cref="IByteChannel"/>。
/// 打印字节流由 T-102 的 <see cref="EscPosBuilder"/> 构造。
/// </summary>
public abstract class LinuxPrinterBase : IReceiptPrinter
{
    private readonly IByteChannel _channel;

    protected LinuxPrinterBase(IByteChannel channel)
    {
        _channel = channel;
    }

    public abstract string Name { get; }
    public abstract PrinterConnection Connection { get; }

    /// <summary>建立连接；返回是否真正连上</summary>
    public virtual async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _channel.ConnectAsync(ct).ConfigureAwait(false);
        return _channel.IsConnected;
    }

    public virtual Task DisconnectAsync() => _channel.DisconnectAsync();

    /// <summary>
    /// 发送 ESC/POS 字节流；若通道已断开则先自动重连。
    /// </summary>
    public virtual async Task PrintAsync(byte[] escposData, CancellationToken ct = default)
    {
        if (escposData == null || escposData.Length == 0)
            return;
        if (!_channel.IsConnected)
            await _channel.ConnectAsync(ct).ConfigureAwait(false); // 断线自动重连
        await _channel.WriteAsync(escposData, ct).ConfigureAwait(false);
    }

    /// <summary>打印测试页（标题 + 字符样张 + 切纸）</summary>
    public virtual async Task<bool> TestAsync(CancellationToken ct = default)
    {
        try
        {
            var data = new EscPosBuilder()
                .Init()
                .Align(AlignStyle.Center)
                .FontSize(2, 2).TextLine("打印测试")
                .FontSize(1, 1)
                .Align(AlignStyle.Left)
                .TextLine("ABCDEFGHIJ 1234567890")
                .TextLine("逸华餐饮收银 测试页")
                .Feed(2)
                .Cut(CutMode.Full)
                .Build();
            await PrintAsync(data, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 查询打印机状态：发送 DLE EOT 1（实时状态），解析回包 Bit3（1=离线）。
    /// 无回包时假设在线；通道未连接时直接返回未连接状态。
    /// </summary>
    public virtual async Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = new PrinterStatus { IsConnected = _channel.IsConnected };
        if (!_channel.IsConnected)
            return status;

        try
        {
            await _channel.WriteAsync(new byte[] { 0x10, 0x04, 0x01 }, ct).ConfigureAwait(false);
            var buf = new byte[1];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            int read = await _channel.ReadAsync(buf, cts.Token).ConfigureAwait(false);
            if (read > 0)
                status.IsOnline = (buf[0] & 0x08) == 0;
            else
                status.IsOnline = true;
        }
        catch
        {
            status.HasError = true;
            status.ErrorMessage = "状态查询失败";
        }
        return status;
    }

    /// <summary>开钱箱（ESC p，走打印机 RJ11 口）</summary>
    public virtual Task OpenCashDrawerAsync(CancellationToken ct = default)
    {
        var data = new EscPosBuilder().OpenDrawer().Build();
        return PrintAsync(data, ct);
    }
}
