using YiHua.Pos.Data;

namespace YiHua.Pos.Tests.Data;

/// <summary>
/// 测试基座：每个测试用例独享一个临时文件数据库，互不干扰，不污染真实数据。
/// xUnit 每个测试方法会创建新的 Fixture 实例，因此每个测试都有全新的空库。
/// </summary>
public abstract class TempDbFixture : IDisposable
{
    protected readonly string DbPath;
    protected readonly PosDbContext Ctx;
    protected readonly DbInitializer Initializer;

    protected TempDbFixture()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yihua-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        DbPath = Path.Combine(dir, "test.db");

        Ctx = new PosDbContext(DbPath);
        Initializer = new DbInitializer(Ctx.Db, DbPath);
        Initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { Ctx.Dispose(); } catch { /* ignore */ }
        try
        {
            var dir = Path.GetDirectoryName(DbPath);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { /* 测试清理失败不影响结果 */ }
        GC.SuppressFinalize(this);
    }
}
