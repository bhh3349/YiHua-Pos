using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>
/// 退款记录。
///
/// 设计要点（这些都是 Wave 5 验收时踩出来的）：
///   ① 退款只累加 Order.AmountRefund，**不回写 AmountPay** ——
///      保留原始应付，财务要能看到客人原本付了多少。
///      因此交班/报表的净额必须显式减去 AmountRefund，不能假设 AmountPay 已扣减。
///   ② ShiftId 记的是**当前班**而不是原班 —— 退款发生在哪个班就计入哪个班，
///      否则跨班退款会让两班账都对不上。
///   ③ Printed 可重复打印：退菜单常要补打，不能打一次就锁死。
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

    /// <summary>补充说明</summary>
    public string? ReasonNote { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    /// <summary>店长授权人（退款超阈值时必填）</summary>
    public string? AuthorizedBy { get; set; }

    /// <summary>计入的班次（当前班，不是原班）</summary>
    public long ShiftId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>是否已打印（退菜单可重复打印）</summary>
    public bool Printed { get; set; }
}