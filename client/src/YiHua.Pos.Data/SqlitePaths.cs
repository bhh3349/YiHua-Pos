namespace YiHua.Pos.Data;

/// <summary>
/// 跨平台数据库路径解析（T-003）。
///
/// 规则：
///   Windows：%LOCALAPPDATA%\YiHua\yihua.db
///   Linux  ：若 /data 目录可写则优先 /data/yihua.db，
///            否则 ~/.local/share/YiHua/yihua.db
/// </summary>
public static class SqlitePaths
{
    public const string DatabaseFileName = "yihua.db";
    public const string AppFolderName = "YiHua";

    /// <summary>按当前操作系统解析数据库文件路径。</summary>
    public static string GetDatabasePath() => GetDatabasePath(ProbeDirectoryWritable);

    /// <summary>
    /// 可测试的纯逻辑版本：调用方注入目录可写性探测，便于单元测试覆盖 Windows / Linux 分支。
    /// </summary>
    public static string GetDatabasePath(Func<string, bool> isWritable)
    {
        if (OperatingSystem.IsWindows())
        {
            var baseDir = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(baseDir, AppFolderName, DatabaseFileName);
        }

        // Linux / 其他 Unix：一体机常见 /data 分区可写时优先使用
        if (isWritable("/data"))
            return Path.Combine("/data", DatabaseFileName);

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", AppFolderName, DatabaseFileName);
    }

    /// <summary>探测目录是否可写：在目录内创建一个零字节临时文件再删除。</summary>
    private static bool ProbeDirectoryWritable(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return false;
            var probe = Path.Combine(dir, $".writetest_{Guid.NewGuid():N}.tmp");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
