using System.Text;
using SqlSugar;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data;

/// <summary>
/// 数据库初始化与维护（实现 <see cref="IDbInitializer"/>，T-003）。
///
/// InitializeAsync 流程：
///   1. 确保数据库目录存在；
///   2. 打开 WAL 模式（PRAGMA journal_mode=WAL，断电保护；该设置持久化到数据库文件）；
///   3. 执行内嵌的 Migrations/V001_Init.sql 建表建索引（幂等，可重复执行）。
/// </summary>
public sealed class DbInitializer : IDbInitializer
{
    private readonly ISqlSugarClient _db;
    private static string? _initSql;

    public DbInitializer(ISqlSugarClient db, string databasePath)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // WAL 模式：写不阻塞读，断电不损坏；持久化设置，执行一次长期有效
        _db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
        // WAL 下 SYNCHRONOUS=NORMAL 即可保证安全，且写入更快
        _db.Ado.ExecuteCommand("PRAGMA synchronous=NORMAL;");

        _db.Ado.ExecuteCommand(LoadInitSql());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// 使用 VACUUM INTO 在线备份（SQLite 3.27+），备份期间不阻塞读写。
    public Task BackupAsync(string targetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("备份目标路径不能为空", nameof(targetPath));

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 先 checkpoint，把 WAL 内容落盘，保证备份完整
        _db.Ado.ExecuteCommand("PRAGMA wal_checkpoint(TRUNCATE);");

        // 路径中的单引号转义（VACUUM INTO 不支持此处用参数绑定）
        var escaped = targetPath.Replace("'", "''");
        _db.Ado.ExecuteCommand($"VACUUM INTO '{escaped}';");
        return Task.CompletedTask;
    }

    private static string LoadInitSql()
    {
        if (_initSql != null)
            return _initSql;

        var resourceName = $"{typeof(DbInitializer).Namespace}.Migrations.V001_Init.sql";
        using var stream = typeof(DbInitializer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"找不到内嵌资源 {resourceName}，请检查 csproj 的 EmbeddedResource 配置");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        _initSql = reader.ReadToEnd();
        return _initSql;
    }
}
