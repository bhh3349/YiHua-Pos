using YiHua.Pos.Data;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.App.Services;

/// <summary>本地数据库备份路径提供者。</summary>
internal sealed class BackupFileProvider : IBackupFileProvider
{
    private readonly PosDbContext _ctx;
    public BackupFileProvider(PosDbContext ctx) => _ctx = ctx;

    public string GetDatabasePath() => _ctx.DatabasePath;

    public string GetBackupDirectory()
    {
        var dir = Path.Combine(Path.GetDirectoryName(_ctx.DatabasePath)!, "backup");
        Directory.CreateDirectory(dir);
        return dir;
    }
}