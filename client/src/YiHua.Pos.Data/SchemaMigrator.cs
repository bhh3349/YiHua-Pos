using SqlSugar;

namespace YiHua.Pos.Data;

/// <summary>
/// 轻量迁移：给已存在的库补列 / 补索引。
///
/// 为什么必须有：V001_Init.sql 用的是 CREATE TABLE IF NOT EXISTS。
/// 对**新建**的库它没问题，但对**已经跑过一版的库**，
/// 表已存在 → 整段 CREATE 被跳过 → 新加的列永远不会出现，
/// 然后查询就报 "no such table: X has no column named Y"。
///
/// SQLite 的 ALTER TABLE ADD COLUMN 不支持 IF NOT EXISTS，
/// 所以先读 pragma table_info 判断列在不在，再决定加不加。
/// 每一步都幂等，重复启动不会出错。
/// </summary>
public static class SchemaMigrator
{
    /// <summary>补齐所有已知的增量列。返回本次实际新增的列（便于日志）。</summary>
    public static List<string> Apply(ISqlSugarClient db)
    {
        var added = new List<string>();

        // 商品：沽清 / 沽清时间 / 时价 / 别名
        // 这四个字段 UI 层一直在用，Domain 里没有对应列，之前只能丢掉。
        EnsureColumn(db, "Product", "IsSoldOut", "INTEGER NOT NULL DEFAULT 0", added);
        EnsureColumn(db, "Product", "SoldOutAt", "TEXT", added);
        EnsureColumn(db, "Product", "IsMarketPrice", "INTEGER NOT NULL DEFAULT 0", added);
        EnsureColumn(db, "Product", "Alias", "TEXT NOT NULL DEFAULT ''", added);

        EnsureIndex(db, "IX_Product_IsSoldOut", "Product", "IsSoldOut");

        return added;
    }

    private static void EnsureColumn(ISqlSugarClient db, string table, string column, string definition, List<string> added)
    {
        if (!TableExists(db, table))
            return;
        if (ColumnExists(db, table, column))
            return;

        db.Ado.ExecuteCommand($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
        added.Add($"{table}.{column}");
    }

    private static void EnsureIndex(ISqlSugarClient db, string indexName, string table, string column)
    {
        if (!TableExists(db, table))
            return;
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS {indexName} ON \"{table}\"({column});");
    }

    private static bool TableExists(ISqlSugarClient db, string table)
    {
        var count = db.Ado.GetInt(
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}';");
        return count > 0;
    }

    private static bool ColumnExists(ISqlSugarClient db, string table, string column)
    {
        // 用 pragma_table_info 表函数查，不要用 PRAGMA table_info + GetDataTable：
        // SqlSugar 的 SQLite 适配器填充 DataTable 时，遇到 dflt_value 列
        // 会报 "Couldn't store <> in dflt_value Column. Expected type is Byte[]"。
        try
        {
            var count = db.Ado.GetInt(
                $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE lower(name)=lower('{column}');");
            return count > 0;
        }
        catch
        {
            // 极老的 SQLite 没有 pragma_table_info 表函数：退化为解析建表语句。
            // 这条路径只在异常时走，正常库不会用到。
            var sql = db.Ado.GetString(
                $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{table}';");
            return sql != null && sql.Contains(column, StringComparison.OrdinalIgnoreCase);
        }
    }
}