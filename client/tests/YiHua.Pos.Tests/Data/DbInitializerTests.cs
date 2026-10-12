using Xunit;
using SqlSugar;
using YiHua.Pos.Data;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Tests.Data;

public class DbInitializerTests : TempDbFixture
{
    [Fact]
    public async Task Initialize_CreatesAllTables()
    {
        var tables = await Ctx.Db.Ado.GetDataTableAsync(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");

        var names = tables.Rows.Cast<System.Data.DataRow>().Select(r => r["name"].ToString()).ToHashSet();
        foreach (var expected in new[] { "Order", "OrderItem", "Payment", "Product", "ProductCategory",
                                          "ProductUnit", "DeskArea", "Desk", "Shift", "OutboxItem", "DictEntry" })
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public async Task Initialize_CreatesUniqueIndexOnClientOrderNo()
    {
        var indexes = await Ctx.Db.Ado.GetDataTableAsync(
            "SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='Order'");
        var names = indexes.Rows.Cast<System.Data.DataRow>().Select(r => r["name"].ToString()).ToList();

        Assert.Contains("UX_Order_ClientOrderNo", names);

        var createSql = indexes.Rows.Cast<System.Data.DataRow>()
            .First(r => r["name"].ToString() == "UX_Order_ClientOrderNo")["sql"].ToString();
        Assert.Contains("UNIQUE", createSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_SetsWalMode()
    {
        // WAL 模式持久化到数据库文件，InitializeAsync 执行后即生效
        var mode = Ctx.Db.Ado.GetString("PRAGMA journal_mode;");
        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task BackupAsync_CreatesValidCopy()
    {
        // 先写入一点数据
        var repo = new ProductRepository(Ctx.Db);
        await repo.ReplaceAllAsync(new[] { new Product { Name = "测试商品", Price = 9.9m } });

        var backupPath = Path.Combine(Path.GetTempPath(), "yihua-tests", Guid.NewGuid().ToString("N"), "backup.db");
        try
        {
            await Initializer.BackupAsync(backupPath);

            Assert.True(File.Exists(backupPath));

            // 备份文件是合法的 SQLite 库，且数据一致
            using var backupCtx = new PosDbContext(backupPath);
            var count = await backupCtx.Db.Queryable<Product>().CountAsync();
            Assert.Equal(1, count);
            var p = await backupCtx.Db.Queryable<Product>().FirstAsync();
            Assert.Equal("测试商品", p!.Name);
            Assert.Equal(9.9m, p.Price);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(backupPath)!, recursive: true); } catch { }
        }
    }

    [Fact]
    public void SqlitePaths_Linux_ResolvesExpectedLocations()
    {
        // 注入 isWindows: false，使测试在任意操作系统上都能覆盖 Linux 分支
        // /data 可写时优先 /data
        var withData = SqlitePaths.GetDatabasePath(_ => true, isWindows: false);
        Assert.Equal(Path.Combine("/data", "yihua.db"), withData);

        // /data 不可写时走 ~/.local/share/YiHua/
        var withoutData = SqlitePaths.GetDatabasePath(_ => false, isWindows: false);
        var home = Environment.GetEnvironmentVariable("HOME")
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".local", "share", "YiHua", "yihua.db"), withoutData);
    }

    [Fact]
    public void SqlitePaths_Windows_ResolvesLocalAppData()
    {
        // 注入 isWindows: true，使测试在任意操作系统上都能覆盖 Windows 分支
        var path = SqlitePaths.GetDatabasePath(_ => true, isWindows: true);

        Assert.EndsWith(Path.Combine("YiHua", "yihua.db"), path);
        Assert.DoesNotContain("/data", path);
    }
}
