using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.UI.Services.HangOrder;

/// <summary>
/// 挂单/拆单计算器（纯逻辑）。
/// 金额全程 decimal，取整显式 MidpointRounding.AwayFromZero。
/// </summary>
public static class HangOrderCalculator
{
    public static readonly MidpointRounding RoundingMode = MidpointRounding.AwayFromZero;

    public static decimal Round2(decimal value) =>
        Math.Round(value, 2, RoundingMode);

    /// <summary>
    /// 校验拆单选择是否合法。
    /// </summary>
    /// <param name="order">原订单</param>
    /// <param name="itemIds">本次要拆出的明细ID</param>
    /// <param name="alreadySettledIds">已预结账的明细ID</param>
    /// <param name="reason">拒绝原因</param>
    public static bool ValidateSplit(
        Order order,
        IReadOnlyList<long> itemIds,
        IReadOnlyCollection<long> alreadySettledIds,
        out string? reason)
    {
        reason = null;
        if (order == null) { reason = "订单为空"; return false; }
        if (itemIds == null || itemIds.Count == 0) { reason = "请选择要拆出的菜品"; return false; }

        var alreadySet = new HashSet<long>(alreadySettledIds ?? Array.Empty<long>());
        var items = order.Items.Where(i => itemIds.Contains(i.Id)).ToList();

        if (items.Count != itemIds.Count) { reason = "选择的菜品不在订单中"; return false; }
        if (items.Any(i => alreadySet.Contains(i.Id))) { reason = "所选菜品已结过账"; return false; }

        decimal splitAmount = CalcSplitAmount(items);
        decimal remaining = CalcRemaining(order, alreadySet, itemIds);

        if (splitAmount <= 0) { reason = "拆出金额必须大于 0"; return false; }
        if (remaining < 0) { reason = "拆单后剩余金额不能为负数"; return false; }

        decimal total = CalcOrderTotal(order);
        if (splitAmount > total) { reason = "拆出金额不能大于订单总额"; return false; }

        return true;
    }

    /// <summary>计算拆出金额（选中明细的金额之和）</summary>
    public static decimal CalcSplitAmount(IEnumerable<OrderItem> items)
    {
        decimal sum = 0m;
        foreach (var i in items)
        {
            if (i.IsGift || i.IsRefunded) continue;
            sum += i.Amount;
        }
        return Round2(sum);
    }

    /// <summary>计算拆单后剩余金额</summary>
    public static decimal CalcRemaining(
        Order order,
        IReadOnlyCollection<long> alreadySettledIds,
        IReadOnlyList<long> newSplitIds)
    {
        var settled = new HashSet<long>(alreadySettledIds ?? Array.Empty<long>());
        foreach (var id in newSplitIds) settled.Add(id);

        decimal remaining = 0m;
        foreach (var i in order.Items)
        {
            if (settled.Contains(i.Id)) continue;
            if (i.IsGift || i.IsRefunded) continue;
            remaining += i.Amount;
        }
        return Round2(remaining);
    }

    /// <summary>订单总额（有效明细之和）</summary>
    public static decimal CalcOrderTotal(Order order)
    {
        decimal sum = 0m;
        foreach (var i in order.Items)
        {
            if (i.IsGift || i.IsRefunded) continue;
            sum += i.Amount;
        }
        return Round2(sum);
    }

    /// <summary>
    /// 拆单后订单状态：全部结完 → Settled，否则 Pending。
    /// 剩余 0 元也视为 Settled。
    /// </summary>
    public static OrderStatus CalcStatusAfterSplit(
        Order order,
        IReadOnlyCollection<long> settledItemIds)
    {
        var settled = new HashSet<long>(settledItemIds ?? Array.Empty<long>());
        bool hasUnsettled = order.Items.Any(i =>
            !settled.Contains(i.Id) && !i.IsGift && !i.IsRefunded);
        return hasUnsettled ? OrderStatus.Pending : OrderStatus.Settled;
    }

    /// <summary>挂单是否超时（边界：刚好等于 timeout 视为超时）</summary>
    public static bool IsTimedOut(HangOrderRecord record, TimeSpan timeout)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        return (DateTime.Now - record.ParkTime) >= timeout;
    }

    /// <summary>
    /// 取单时桌台冲突检查。
    /// 原桌空闲 → 可直接恢复；被占 → 需换桌。
    /// </summary>
    public static bool CanRecall(
        HangOrderRecord record,
        Desk desk,
        out string? reason)
    {
        reason = null;
        if (record == null) { reason = "挂单记录为空"; return false; }
        if (desk == null) { reason = "桌台为空"; return false; }

        if (desk.Id == record.DeskId)
        {
            // 原桌
            if (desk.Status == DeskStatus.Idle) return true;
            reason = $"桌台 {desk.Name} 已被占用，可换到其他空闲桌台";
            return false;
        }

        // 换桌：目标桌必须空闲
        if (desk.Status != DeskStatus.Idle)
        {
            reason = $"桌台 {desk.Name} 不是空闲状态";
            return false;
        }
        return true;
    }
}
