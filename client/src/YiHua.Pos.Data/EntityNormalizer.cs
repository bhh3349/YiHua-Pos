using System.Reflection;

namespace YiHua.Pos.Data;

/// <summary>
/// 实体规范化（T-003）。
///
/// 任务卡要求时间统一以 'yyyy-MM-dd HH:mm:ss' 字符串存储。
/// Microsoft.Data.Sqlite 默认会带毫秒（.FFFFFFF），因此在入库前把所有
/// DateTime / DateTime? 属性截断到秒。采用反射实现，Domain 后续新增
/// 时间字段时无需改 Data 层代码。
/// </summary>
internal static class EntityNormalizer
{
    public static T TruncateDates<T>(T entity) where T : class?
    {
        if (entity == null)
            return entity;

        foreach (var prop in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            if (prop.PropertyType == typeof(DateTime))
            {
                var dt = (DateTime)prop.GetValue(entity)!;
                prop.SetValue(entity, Truncate(dt));
            }
            else if (prop.PropertyType == typeof(DateTime?))
            {
                if (prop.GetValue(entity) is DateTime dt)
                    prop.SetValue(entity, Truncate(dt));
            }
        }
        return entity;
    }

    public static DateTime Truncate(DateTime dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);

    /// <summary>当前时间（已截断到秒），供仓储层生成时间戳使用。</summary>
    public static DateTime Now => Truncate(DateTime.Now);
}
