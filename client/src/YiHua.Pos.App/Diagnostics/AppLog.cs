using System.Text;

namespace YiHua.Pos.App.Diagnostics;

/// <summary>
/// 极简文件日志。
///
/// 为什么必须有：OutputType=WinExe，没有控制台，Trace 输出无处可看。
/// 之前页面抛异常，进程直接静默消失，商家看到的是「双击没反应」，
/// 开发者这边什么都抓不到 —— 这次 87 个界面接线就是这么被拖了很久。
///
/// 路径与数据库同目录：%LOCALAPPDATA%\YiHua\logs\app-yyyyMMdd.log
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();
    private static string _dir = string.Empty;
    private static bool _installed;

    /// <summary>当前日志文件路径（诊断报告要引用它）。</summary>
    public static string CurrentFile { get; private set; } = string.Empty;

    /// <summary>安装全局异常捕获。幂等，可在多处安全调用。</summary>
    public static void Install()
    {
        // 已装过就直接返回：Initialize 和 Main 都可能调
        lock (Gate)
        {
            if (_installed)
                return;
            _installed = true;
        }

        try
        {
            var baseDir = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = AppContext.BaseDirectory;

            _dir = Path.Combine(baseDir, "YiHua", "logs");
            Directory.CreateDirectory(_dir);

            var file = Path.Combine(_dir, $"app-{DateTime.Now:yyyyMMdd}.log");
            CurrentFile = file;

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write("FATAL", $"未捕获异常: {e.ExceptionObject}");

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Write("ERROR", $"未观察到的任务异常: {e.Exception}");
                e.SetObserved();
            };

            Write("INFO", $"程序启动 {Environment.Version} / {Environment.OSVersion}");
        }
        catch
        {
            // 日志系统本身不能把程序搞崩
        }
    }

    /// <summary>供 Avalonia UI 线程异常使用，需在 Avalonia 初始化后才可调用。</summary>
    public static void AttachUiThreadHandler()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
                Write("FATAL", $"UI 线程异常: {e.Exception}");
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>写一行日志。永不抛异常。</summary>
    public static void Write(string level, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_dir))
                return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(_dir);
                var file = Path.Combine(_dir, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, line, Encoding.UTF8);
                CurrentFile = file;
            }
        }
        catch
        {
            // 磁盘满、权限不足等情况下放弃写日志，绝不影响主流程
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);
}