using SqlSugar;

namespace YiHua.Pos.Data;

/// <summary>
/// 事务辅助（T-003）。
///
/// 背景：SqlSugar 的 <c>Ado.UseTranAsync</c> 在回调抛异常时不会 rethrow，
/// 而是返回 <c>DbResult{ IsSuccess=false }</c>（即使 errorCallBack 为 null）。
/// 直接使用会导致约束冲突等异常被静默吞掉。本 helper 在失败时抛出原始异常，
/// 保证调用方能感知失败（如 ClientOrderNo 重复）。
/// </summary>
internal static class Tran
{
    public static async Task RunAsync(ISqlSugarClient db, Func<Task> action)
    {
        var result = await db.Ado.UseTranAsync(action);
        if (!result.IsSuccess)
            throw result.ErrorException
                ?? new InvalidOperationException("事务执行失败: " + result.ErrorMessage);
    }
}
