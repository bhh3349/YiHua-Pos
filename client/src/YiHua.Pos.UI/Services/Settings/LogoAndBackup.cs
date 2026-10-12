namespace YiHua.Pos.UI.Services.Settings;

/// <summary>
/// Logo 处理：校验/压缩。
/// 小票打印是位图，太大打印会卡，所以要压缩。
/// </summary>
public static class LogoProcessor
{
    public const int MaxSizeBytes = 200 * 1024; // 200KB
    public const int MaxWidth = 400;  // 小票宽度限制
    public const int MaxHeight = 200;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp",
    };

    /// <summary>
    /// 校验 logo（大小/格式）。
    /// </summary>
    public static bool Validate(byte[]? data, string fileName, out string? error)
    {
        if (data == null || data.Length == 0)
        {
            error = "请选择一张图片";
            return false;
        }
        if (data.Length > MaxSizeBytes)
        {
            error = $"图片太大了（{data.Length / 1024}KB），请压缩到 200KB 以内再上传。小票打印太大的图会很卡。";
            return false;
        }
        var ext = Path.GetExtension(fileName);
        if (!SupportedExtensions.Contains(ext))
        {
            error = $"不支持 {ext} 格式，请用 PNG 或 JPG";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 压缩说明：实际压缩由 App 层用 SkiaSharp 完成，
    /// 此处返回目标尺寸（宽 400px 以内等比缩放）。
    /// </summary>
    public static (int Width, int Height) GetTargetSize(int originalWidth, int originalHeight)
    {
        if (originalWidth <= MaxWidth && originalHeight <= MaxHeight)
            return (originalWidth, originalHeight);

        var ratio = Math.Min((double)MaxWidth / originalWidth, (double)MaxHeight / originalHeight);
        return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
    }
}

/// <summary>
/// 备份服务：备份/恢复数据库文件。
/// 恢复前必须二次确认 + 自动备份当前数据。
/// </summary>
public class BackupService
{
    private readonly IBackupFileProvider _fileProvider;

    public BackupService(IBackupFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
    }

    /// <summary>
    /// 一键备份：复制数据库文件到备份目录。
    /// 返回备份文件路径。
    /// </summary>
    public async Task<string> BackupAsync(CancellationToken ct = default)
    {
        var dbPath = _fileProvider.GetDatabasePath();
        if (!File.Exists(dbPath))
            throw new FileNotFoundException("数据库文件不存在，可能是首次启动，还没有数据可备份。", dbPath);

        var backupDir = _fileProvider.GetBackupDirectory();
        Directory.CreateDirectory(backupDir);

        var backupPath = Path.Combine(backupDir, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        await Task.Run(() => File.Copy(dbPath, backupPath, overwrite: false), ct);
        return backupPath;
    }

    /// <summary>
    /// 恢复前检查：返回 (需要二次确认的原因, 是否会自动备份当前数据)。
    /// </summary>
    public (string ConfirmMessage, bool WillBackupCurrent) CheckBeforeRestore(string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("备份文件不存在或已损坏，请检查文件路径。", backupPath);

        // 简单校验：SQLite 文件头
        var header = new byte[16];
        using (var fs = File.OpenRead(backupPath))
        {
            if (fs.Length < 100)
                throw new InvalidDataException("备份文件太小，可能已损坏，无法恢复。");
            fs.Read(header, 0, 16);
        }
        var magic = System.Text.Encoding.ASCII.GetString(header);
        if (!magic.StartsWith("SQLite format 3"))
            throw new InvalidDataException("备份文件不是有效的数据库文件，可能已损坏，无法恢复。");

        var msg = "恢复会用备份文件替换当前数据，当前未备份的数据将丢失。\n" +
                  "系统会自动先备份当前数据，确认要继续吗？";
        return (msg, true);
    }

    /// <summary>
    /// 执行恢复：先备份当前数据，再替换。
    /// </summary>
    public async Task<string> RestoreAsync(string backupPath, CancellationToken ct = default)
    {
        // 1. 先校验
        CheckBeforeRestore(backupPath);

        // 2. 自动备份当前数据
        string autoBackupPath = "";
        var dbPath = _fileProvider.GetDatabasePath();
        if (File.Exists(dbPath))
        {
            autoBackupPath = await BackupAsync(ct);
        }

        // 3. 替换
        await Task.Run(() => File.Copy(backupPath, dbPath, overwrite: true), ct);
        return autoBackupPath;
    }
}

/// <summary>
/// 备份文件提供者（App 层实现）。
/// </summary>
public interface IBackupFileProvider
{
    string GetDatabasePath();
    string GetBackupDirectory();
}
