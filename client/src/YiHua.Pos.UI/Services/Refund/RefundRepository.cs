using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.Services.Refund;

// RefundRecord / RefundType / IRefundRepository 已上移到 Domain（Wave 5 补充）。
// 这里直接用 Domain 的类型，避免同一张表出现两份互相不兼容的类定义。

/// <summary>内存假实现（测试用；生产实现待 Data 层）</summary>
public class InMemoryRefundRepository : IRefundRepository
{
    private readonly List<RefundRecord> _records = new();
    private long _nextId = 1;

    public Task<long> InsertAsync(RefundRecord record, CancellationToken ct = default)
    {
        record.Id = _nextId++;
        _records.Add(record);
        return Task.FromResult(record.Id);
    }

    public Task<IReadOnlyList<RefundRecord>> GetByOrderIdAsync(long orderId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RefundRecord>>(_records.Where(r => r.OrderId == orderId).ToList());

    public Task<decimal> GetTotalRefundedAsync(long orderId, CancellationToken ct = default) =>
        Task.FromResult(_records.Where(r => r.OrderId == orderId).Sum(r => r.Amount));

    /// <summary>按班次查退款（交班对账用：退款计入发生的那个班）</summary>
    public Task<IReadOnlyList<RefundRecord>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RefundRecord>>(_records.Where(r => r.ShiftId == shiftId).ToList());

    public Task MarkPrintedAsync(long recordId, CancellationToken ct = default)
    {
        var r = _records.FirstOrDefault(x => x.Id == recordId);
        if (r != null)
            r.Printed = true;
        return Task.CompletedTask;
    }
}
