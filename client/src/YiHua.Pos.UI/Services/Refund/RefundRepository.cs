using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Services.Refund;

/// <summary>
/// 退款记录（UI 层模型；Domain 缺口：RefundRecord 实体、RefundType 枚举）。
/// 记录：退哪单、退哪行、退多少、原因、操作人、时间。
/// </summary>
public class RefundRecord
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>退菜时为明细行 ID；整单退时为 0</summary>
    public long OrderItemId { get; set; }

    public RefundType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReasonNote { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    /// <summary>店长授权人（超阈值时必填）</summary>
    public string? AuthorizedBy { get; set; }

    /// <summary>计入的班次（当前班，不是原班）</summary>
    public long ShiftId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>是否已打印（退菜单可重复打印）</summary>
    public bool Printed { get; set; }
}

/// <summary>退款仓储接口（可注入，测试用假实现）</summary>
public interface IRefundRepository
{
    Task<long> InsertAsync(RefundRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<RefundRecord>> GetByOrderIdAsync(long orderId, CancellationToken ct = default);
    Task<decimal> GetTotalRefundedAsync(long orderId, CancellationToken ct = default);
    Task MarkPrintedAsync(long recordId, CancellationToken ct = default);
}

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

    public Task MarkPrintedAsync(long recordId, CancellationToken ct = default)
    {
        var r = _records.FirstOrDefault(x => x.Id == recordId);
        if (r != null)
            r.Printed = true;
        return Task.CompletedTask;
    }
}
