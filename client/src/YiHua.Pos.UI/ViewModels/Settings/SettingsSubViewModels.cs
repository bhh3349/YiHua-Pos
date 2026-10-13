using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.UI.ViewModels.Settings;

/// <summary>
/// 系统信息 ViewModel。
/// </summary>
public class SystemInfoViewModel
{
    private readonly IBackupFileProvider _fileProvider;
    private readonly DiagnosticInfoProvider _diagProvider;

    public SystemInfoViewModel(IBackupFileProvider fileProvider, DiagnosticInfoProvider diagProvider)
    {
        _fileProvider = fileProvider;
        _diagProvider = diagProvider;
    }

    public string AppVersion => _diagProvider.AppVersion;
    public string OSDescription => _diagProvider.OSDescription;
    public string DatabasePath => _fileProvider.GetDatabasePath();

    public string? BackupMessage { get; private set; }
    public string? RestoreMessage { get; private set; }
    public bool IsWorking { get; private set; }

    public async Task<string> BackupAsync(CancellationToken ct = default)
    {
        IsWorking = true;
        try
        {
            var service = new BackupService(_fileProvider);
            var path = await service.BackupAsync(ct);
            BackupMessage = $"备份成功：{path}";
            return path;
        }
        catch (Exception ex)
        {
            BackupMessage = $"备份失败：{ex.Message}";
            throw;
        }
        finally { IsWorking = false; }
    }

    public (string ConfirmMessage, bool WillBackup) CheckRestore(string backupPath)
    {
        var service = new BackupService(_fileProvider);
        return service.CheckBeforeRestore(backupPath);
    }
}

/// <summary>
/// 诊断报告 ViewModel。
/// </summary>
public class DiagnosticReportViewModel
{
    private readonly DiagnosticInfoProvider _provider;

    public DiagnosticReportViewModel(DiagnosticInfoProvider provider)
    {
        _provider = provider;
    }

    public string ReportContent { get; private set; } = string.Empty;
    public bool IsGenerating { get; private set; }
    public string? ErrorMessage { get; private set; }

    public Task GenerateAsync()
    {
        IsGenerating = true;
        ErrorMessage = null;
        try
        {
            var builder = new DiagnosticReportBuilder(_provider);
            ReportContent = builder.Build();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"生成诊断报告失败：{ex.Message}";
        }
        finally { IsGenerating = false; }
        return Task.CompletedTask;
    }

    public async Task SaveToFileAsync(string path)
    {
        await File.WriteAllTextAsync(path, ReportContent);
    }
}

/// <summary>
/// 网络同步 ViewModel。
/// </summary>
public class NetworkSyncViewModel : SettingsViewModelBase
{
    private readonly ISettingsStore _store;

    public NetworkSyncViewModel(ISettingsStore store)
    {
        _store = store;
    }

    public string ServerAddress { get; set; } = string.Empty;
    public bool IsOnline { get; private set; }
    public int PendingCount { get; private set; }
    public DateTime? LastSyncTime { get; private set; }
    public string? SyncError { get; private set; }
    public bool IsSyncing { get; private set; }

    public string? ValidationError { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var info = await _store.GetSyncInfoAsync(ct);
        ServerAddress = info.ServerAddress;
        IsOnline = info.IsOnline;
        PendingCount = info.PendingCount;
        LastSyncTime = info.LastSyncTime;
        SyncError = info.LastError;
        MarkSaved();
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (!SettingsValidator.ValidateServerAddress(ServerAddress, out var error))
        {
            ValidationError = error;
            return false;
        }
        ValidationError = null;

        MarkSaving();
        try
        {
            await _store.SaveSyncInfoAsync(new SyncInfoDto
            {
                ServerAddress = ServerAddress.Trim(),
                IsOnline = IsOnline,
                PendingCount = PendingCount,
                LastSyncTime = LastSyncTime,
            }, ct);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            MarkFailed($"保存失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 手动触发同步（可测试状态变化）。
    /// </summary>
    public async Task ManualSyncAsync(Func<CancellationToken, Task> syncFunc, CancellationToken ct = default)
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncError = null;
        try
        {
            await syncFunc(ct);
            LastSyncTime = DateTime.Now;
            PendingCount = 0;
        }
        catch (Exception ex)
        {
            // 网络断开时不卡住，显示离线
            SyncError = $"同步失败：{ex.Message}。已切换为离线模式，数据保存在本地。";
            IsOnline = false;
        }
        finally { IsSyncing = false; }
    }
}

/// <summary>
/// 设置导航 ViewModel。
/// </summary>
public class SettingsNavigationViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private SettingsNavItem? _selected;
    private object? _selectedPage;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<SettingsNavItem>? SelectionChanged;

    public List<SettingsNavItem> Items { get; } = new()
    {
        new("门店信息", "店名/地址/电话/logo"),
        new("打印机管理", "配完能测试打印"),
        new("收银规则", "抹零/折扣/小票内容"),
        new("账号与权限", "复用 T-301"),
        new("系统信息与备份", "一键备份/恢复/诊断"),
        new("网络与同步", "服务器地址/同步状态"),
    };

    public virtual SettingsNavItem? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected)));
            SelectionChanged?.Invoke(this, value!);
        }
    }

    public object? SelectedPage
    {
        get => _selectedPage;
        protected set
        {
            _selectedPage = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedPage)));
        }
    }
}

public class SettingsNavItem
{
    public SettingsNavItem(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }
    public string Title { get; }
    public string Subtitle { get; }

    /// <summary>
    /// 返回标题。
    /// 不重写的话 ListBox 会把整个类型名当成这一项的名字 ——
    /// 屏幕阅读器念出来是「YiHua.Pos.UI.ViewModels.Settings.SettingsNavItem」，
    /// 自动化测试也点不中这一项。
    /// </summary>
    public override string ToString() => Title;
}
