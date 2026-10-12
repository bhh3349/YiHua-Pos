using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.ViewModels.Desk;

/// <summary>状态转换结果</summary>
public class TransitionResult
{
    public bool Ok { get; }
    public string Error { get; }

    private TransitionResult(bool ok, string error) { Ok = ok; Error = error; }
    public static TransitionResult Success() => new(true, string.Empty);
    public static TransitionResult Fail(string error) => new(false, error);
}

/// <summary>
/// 桌台状态机：用显式转换方法表达合法流转，非法转换返回明确错误。
/// 空闲 ─开台→ 占用 ─结账+清台→ 空闲
/// 空闲 ─预订→ 预订 ─到店开台→ 占用
/// 预订 ─取消预订→ 空闲
/// </summary>
public static class DeskStatusTransitions
{
    /// <summary>开台：空闲→占用；预订→占用（客人到店）</summary>
    public static TransitionResult TryOpen(DeskEntity desk) =>
        desk.Status switch
        {
            DeskStatus.Idle => TransitionResult.Success(),
            DeskStatus.Reserved => TransitionResult.Success(),
            DeskStatus.Occupied => TransitionResult.Fail($"桌台 {desk.Name} 已在用，不能重复开台"),
            _ => TransitionResult.Fail($"桌台 {desk.Name} 状态异常，不能开台"),
        };

    /// <summary>预订：仅空闲可预订</summary>
    public static TransitionResult TryReserve(DeskEntity desk) =>
        desk.Status == DeskStatus.Idle
            ? TransitionResult.Success()
            : TransitionResult.Fail($"桌台 {desk.Name} 不是空闲状态，不能预订");

    /// <summary>取消预订：预订→空闲</summary>
    public static TransitionResult TryCancelReserve(DeskEntity desk) =>
        desk.Status == DeskStatus.Reserved
            ? TransitionResult.Success()
            : TransitionResult.Fail($"桌台 {desk.Name} 没有预订，无需取消");

    /// <summary>
    /// 换台：源桌必须占用，目标桌必须空闲；
    /// 源桌有已结账未清的明细 → 拒绝，先清台。
    /// </summary>
    public static TransitionResult TryChange(DeskEntity source, DeskEntity target, bool sourceHasUnsettledItems)
    {
        if (source.Status != DeskStatus.Occupied)
            return TransitionResult.Fail($"源桌 {source.Name} 未在使用，不能换台");
        if (target.Status != DeskStatus.Idle)
            return TransitionResult.Fail($"目标桌 {target.Name} 不是空闲，不能换入");
        if (sourceHasUnsettledItems)
            return TransitionResult.Fail($"源桌 {source.Name} 有已结账未清的明细，请先清台");
        if (source.Id == target.Id)
            return TransitionResult.Fail("源桌和目标桌不能是同一张桌");
        return TransitionResult.Success();
    }

    /// <summary>并台：两桌都必须占用</summary>
    public static TransitionResult TryMerge(DeskEntity a, DeskEntity b)
    {
        if (a.Status != DeskStatus.Occupied)
            return TransitionResult.Fail($"桌台 {a.Name} 未在使用，不能并台");
        if (b.Status != DeskStatus.Occupied)
            return TransitionResult.Fail($"桌台 {b.Name} 未在使用，不能并台");
        if (a.Id == b.Id)
            return TransitionResult.Fail("不能与自己并台");
        return TransitionResult.Success();
    }

    /// <summary>清台：必须已结账；预订桌不能直接清台</summary>
    public static TransitionResult TryClear(DeskEntity desk, bool isSettled) =>
        desk.Status switch
        {
            DeskStatus.Reserved => TransitionResult.Fail($"预订桌 {desk.Name} 不能直接清台，请先取消预订"),
            DeskStatus.Idle => TransitionResult.Fail($"桌台 {desk.Name} 已经是空闲，无需清台"),
            DeskStatus.Occupied when !isSettled => TransitionResult.Fail($"桌台 {desk.Name} 尚未结账，请先结账再清台"),
            DeskStatus.Occupied => TransitionResult.Success(),
            _ => TransitionResult.Fail($"桌台 {desk.Name} 状态异常，不能清台"),
        };
}
