using System.Text;
using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.UI.Services.HangOrder;

/// <summary>
/// 挂单/取单打印服务。
/// 打印机不可用时静默跳过（不阻断业务流程）。
/// </summary>
public sealed class HangOrderPrintService
{
    private readonly IReceiptPrinter? _printer;

    public HangOrderPrintService(IReceiptPrinter? printer = null)
    {
        _printer = printer;
    }

    /// <summary>打印挂单通知（厨房联）</summary>
    public async Task PrintParkNoticeAsync(HangOrderRecord record, CancellationToken ct = default)
    {
        if (_printer == null) return;
        string text = BuildParkNotice(record);
        await _printer.PrintAsync(Encoding.UTF8.GetBytes(text), ct).ConfigureAwait(false);
    }

    /// <summary>打印取单通知</summary>
    public async Task PrintRecallNoticeAsync(HangOrderRecord record, string deskName, CancellationToken ct = default)
    {
        if (_printer == null) return;
        string text = BuildRecallNotice(record, deskName);
        await _printer.PrintAsync(Encoding.UTF8.GetBytes(text), ct).ConfigureAwait(false);
    }

    private static string BuildParkNotice(HangOrderRecord r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== 挂单通知 ====");
        sb.AppendLine($"桌号：{r.DeskName}");
        sb.AppendLine($"人数：{r.GuestCount}");
        sb.AppendLine($"金额：¥{r.Amount:F2}");
        sb.AppendLine($"时间：{r.ParkTime:HH:mm}");
        sb.AppendLine($"挂单人：{r.OperatorName}");
        if (!string.IsNullOrWhiteSpace(r.Remark))
            sb.AppendLine($"备注：{r.Remark}");
        sb.AppendLine("==================");
        return sb.ToString();
    }

    private static string BuildRecallNotice(HangOrderRecord r, string deskName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== 取单通知 ====");
        sb.AppendLine($"原桌号：{r.DeskName}");
        sb.AppendLine($"现桌号：{deskName}");
        sb.AppendLine($"金额：¥{r.Amount:F2}");
        sb.AppendLine($"时间：{DateTime.Now:HH:mm}");
        sb.AppendLine("==================");
        return sb.ToString();
    }
}
