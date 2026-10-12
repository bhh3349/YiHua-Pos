using SqlSugar;
using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.Data;

/// <summary>
/// 数据库上下文：持有指向本地 SQLite 文件的 SqlSugarClient（T-003）。
///
/// 约定：
///   * 一个数据库文件对应一个 PosDbContext 实例（SqlSugarClient 线程安全，可单例）；
///   * 仓储类通过构造函数注入 <see cref="ISqlSugarClient"/>，便于单元测试替换为临时库；
///   * Domain 实体已冻结，无法添加 [SugarColumn] 特性：Order.Items / Order.Payments
///     这两个聚合导航属性通过 EntityService 从列映射中忽略，由仓储层手动分步加载。
/// </summary>
public sealed class PosDbContext : IDisposable
{
    public ISqlSugarClient Db { get; }

    public string DatabasePath { get; }

    public PosDbContext(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("数据库路径不能为空", nameof(databasePath));

        DatabasePath = databasePath;

        // SQLite 不会自动创建目录，目录不存在时建库会报
        // "unable to open database file"（Error 14），首次启动必现。
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            // 实体无特性标记：主键/自增通过下方 EntityService 集中声明，
            // 保持与 SqlSugar 默认约定一致（名为 Id 的 long 属性即主键自增）
            InitKeyType = InitKeyType.Attribute,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (prop, column) =>
                {
                    // 无 setter 的计算属性（如 Product.IsOnSale、Desk.IsIdle）不是数据库列
                    if (!prop.CanWrite)
                    {
                        column.IsIgnore = true;
                        return;
                    }

                    // 实体冻结无法加特性，在此集中声明主键，避免 Insert 时把 Id=0 当显式值写入
                    if (column.PropertyName == "Id")
                    {
                        column.IsPrimarykey = true;
                        column.IsIdentity = true;
                    }

                    // Order.Items / Order.Payments 为聚合导航属性（仓储层手动加载），
                    // 不是数据库列；Domain 冻结无法加特性，在此集中忽略
                    if (prop.DeclaringType == typeof(Order) &&
                        (column.PropertyName == nameof(Order.Items) ||
                         column.PropertyName == nameof(Order.Payments)))
                    {
                        column.IsIgnore = true;
                    }
                },
            },
        });

        Db = db;
    }

    /// <summary>按跨平台规则解析路径并创建上下文（生产入口）。</summary>
    public static PosDbContext CreateDefault() => new(SqlitePaths.GetDatabasePath());

    public void Dispose() => Db?.Dispose();
}
