using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class LogoProcessorTests
{
    [Fact]
    public void Validate_Empty_Rejected()
    {
        Assert.False(LogoProcessor.Validate(null, "logo.png", out var error));
        Assert.Contains("选择", error);
        Assert.False(LogoProcessor.Validate(Array.Empty<byte>(), "logo.png", out _));
    }

    [Fact]
    public void Validate_TooLarge_Rejected()
    {
        var big = new byte[300 * 1024]; // 300KB
        Assert.False(LogoProcessor.Validate(big, "logo.png", out var error));
        Assert.Contains("太大", error);
        Assert.Contains("200KB", error);
    }

    [Fact]
    public void Validate_UnsupportedFormat_Rejected()
    {
        var data = new byte[1024];
        Assert.False(LogoProcessor.Validate(data, "logo.gif", out var error));
        Assert.Contains("不支持", error);
    }

    [Fact]
    public void Validate_Valid_Passes()
    {
        var data = new byte[50 * 1024]; // 50KB
        Assert.True(LogoProcessor.Validate(data, "logo.png", out _));
        Assert.True(LogoProcessor.Validate(data, "logo.JPG", out _)); // 大小写不敏感
    }

    [Fact]
    public void GetTargetSize_ScalesDown()
    {
        var (w, h) = LogoProcessor.GetTargetSize(800, 400);
        Assert.True(w <= 400);
        Assert.True(h <= 200);
        Assert.Equal(2.0, (double)w / h, 1); // 保持比例
    }

    [Fact]
    public void GetTargetSize_SmallKeepsOriginal()
    {
        var (w, h) = LogoProcessor.GetTargetSize(200, 100);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }
}

public class BackupRestoreTests
{
    private class TestFileProvider : IBackupFileProvider
    {
        public string DbPath { get; set; } = "";
        public string BackupDir { get; set; } = "";
        public string GetDatabasePath() => DbPath;
        public string GetBackupDirectory() => BackupDir;
    }

    private static void WriteFakeDb(string path)
    {
        // SQLite 文件头
        var header = System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0");
        var data = new byte[200];
        Array.Copy(header, data, header.Length);
        File.WriteAllBytes(path, data);
    }

    [Fact]
    public async Task Backup_CreatesFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var dbPath = Path.Combine(dir, "pos.db");
            WriteFakeDb(dbPath);

            var provider = new TestFileProvider { DbPath = dbPath, BackupDir = Path.Combine(dir, "backups") };
            var service = new BackupService(provider);
            var backupPath = await service.BackupAsync();

            Assert.True(File.Exists(backupPath));
            Assert.Contains("backup_", Path.GetFileName(backupPath));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Backup_NoDatabase_ThrowsFriendly()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var provider = new TestFileProvider
            {
                DbPath = Path.Combine(dir, "nonexistent.db"),
                BackupDir = Path.Combine(dir, "backups"),
            };
            var service = new BackupService(provider);
            var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => service.BackupAsync());
            Assert.Contains("还没有数据", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CheckBeforeRestore_CorruptFile_Rejected()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var badPath = Path.Combine(dir, "bad.db");
            File.WriteAllText(badPath, "not a database");

            var provider = new TestFileProvider { DbPath = "", BackupDir = dir };
            var service = new BackupService(provider);
            var ex = Assert.Throws<InvalidDataException>(() => service.CheckBeforeRestore(badPath));
            Assert.Contains("损坏", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CheckBeforeRestore_Valid_ReturnsConfirmMessage()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var goodPath = Path.Combine(dir, "good.db");
            WriteFakeDb(goodPath);

            var provider = new TestFileProvider { DbPath = "", BackupDir = dir };
            var service = new BackupService(provider);
            var (msg, willBackup) = service.CheckBeforeRestore(goodPath);
            Assert.Contains("替换", msg);
            Assert.True(willBackup);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Restore_BacksUpCurrentFirst()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var dbPath = Path.Combine(dir, "pos.db");
            WriteFakeDb(dbPath);
            var backupPath = Path.Combine(dir, "restore_from.db");
            WriteFakeDb(backupPath);

            var provider = new TestFileProvider { DbPath = dbPath, BackupDir = Path.Combine(dir, "backups") };
            var service = new BackupService(provider);
            var autoBackup = await service.RestoreAsync(backupPath);

            Assert.True(File.Exists(autoBackup)); // 自动备份了当前数据
        }
        finally { Directory.Delete(dir, true); }
    }
}
