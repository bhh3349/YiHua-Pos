using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.Services.Shift;

// 别名必须在 namespace 内部：YiHua.Pos.UI.Services.Shift 子命名空间
// 离引用点更近，文件顶部的 using 会被跳过（Wave 3 踩过，Lead 在 2cab19f 修过）
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;

/// <summary>班次服务接口（可注入，测试用假实现）</summary>
public interface IShiftService
{
    /// <summary>获取当前未关闭的班（无则返回 null）</summary>
    Task<ShiftEntity?> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// 开班：备用金必填；已有未关闭班时拒绝（防重复）。
    /// </summary>
    Task<ShiftEntity> OpenAsync(decimal openingFloat, long operatorId, string operatorName,
        string terminalNo, CancellationToken ct = default);

    /// <summary>
    /// 交班：实点金额 + 差额原因必填；关闭班次，记录存档。
    /// </summary>
    Task CloseAsync(long shiftId, decimal actualCash, DifferenceReason reason, string? reasonNote,
        string operatorName, CancellationToken ct = default);

    /// <summary>
    /// 强制交班：仅店长；记录操作人并标注"强制"。
    /// </summary>
    Task ForceCloseAsync(long shiftId, decimal actualCash, DifferenceReason reason,
        string managerName, CancellationToken ct = default);

    /// <summary>交班历史</summary>
    Task<IReadOnlyList<ShiftEntity>> GetHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>班次服务实现</summary>
public class ShiftService : IShiftService
{
    private readonly IShiftRepository _shiftRepository;

    public ShiftService(IShiftRepository shiftRepository)
    {
        _shiftRepository = shiftRepository;
    }

    public Task<ShiftEntity?> GetCurrentAsync(CancellationToken ct = default) =>
        _shiftRepository.GetCurrentAsync(ct);

    public async Task<ShiftEntity> OpenAsync(decimal openingFloat, long operatorId, string operatorName,
        string terminalNo, CancellationToken ct = default)
    {
        if (openingFloat < 0)
            throw new ArgumentException("备用金不能为负数", nameof(openingFloat));

        var current = await _shiftRepository.GetCurrentAsync(ct).ConfigureAwait(false);
        if (current != null)
            throw new InvalidOperationException(
                $"已有未关闭的班次（{current.OperatorName} 于 {current.StartAt:HH:mm} 开班），不能重复开班");

        var shift = new ShiftEntity
        {
            TerminalNo = terminalNo,
            OperatorId = operatorId,
            OperatorName = operatorName,
            StartAt = DateTime.Now,
            Status = ShiftStatus.Open,
            CashOpen = openingFloat,
        };
        shift.Id = await _shiftRepository.InsertAsync(shift, ct).ConfigureAwait(false);
        return shift;
    }

    public async Task CloseAsync(long shiftId, decimal actualCash, DifferenceReason reason, string? reasonNote,
        string operatorName, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetByIdAsync(shiftId, ct).ConfigureAwait(false);
        if (shift == null)
            throw new InvalidOperationException("班次不存在");
        if (shift.Status != ShiftStatus.Open)
            throw new InvalidOperationException("只能关闭进行中的班次（已关闭的班不允许修改）");

        // 差额 = 实点 - 应有；应有由调用方（ViewModel）用 ShiftCalculator 算好传入 CashShould
        shift.CashActual = actualCash;
        shift.CashDiff = ShiftCalculator.CalculateDifference(actualCash, shift.CashShould);
        shift.EndAt = DateTime.Now;
        shift.Status = ShiftStatus.Closed;
        shift.Remark = BuildRemark(reason, reasonNote, operatorName, forced: false);

        await _shiftRepository.UpdateAsync(shift, ct).ConfigureAwait(false);
    }

    public async Task ForceCloseAsync(long shiftId, decimal actualCash, DifferenceReason reason,
        string managerName, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetByIdAsync(shiftId, ct).ConfigureAwait(false);
        if (shift == null)
            throw new InvalidOperationException("班次不存在");
        if (shift.Status != ShiftStatus.Open)
            throw new InvalidOperationException("只能强制关闭进行中的班次");

        shift.CashActual = actualCash;
        shift.CashDiff = ShiftCalculator.CalculateDifference(actualCash, shift.CashShould);
        shift.EndAt = DateTime.Now;
        shift.Status = ShiftStatus.Closed;
        shift.Remark = BuildRemark(reason, null, managerName, forced: true);

        await _shiftRepository.UpdateAsync(shift, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ShiftEntity>> GetHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = await _shiftRepository.GetHistoryAsync(from, to, ct).ConfigureAwait(false);
        return list;
    }

    private static string BuildRemark(DifferenceReason reason, string? note, string operatorName, bool forced)
    {
        var parts = new List<string>();
        if (forced)
            parts.Add("[强制交班]");
        parts.Add($"操作人：{operatorName}");
        parts.Add($"差额原因：{ShiftDifferencePolicy.DescribeReason(reason)}");
        if (!string.IsNullOrWhiteSpace(note))
            parts.Add(note.Trim());
        return string.Join("；", parts);
    }
}
