using YiHua.Pos.UI.Services.SoldOut;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.SoldOut;

/// <summary>定时设置 ViewModel（说人话，不说术语）</summary>
public sealed class SoldOutScheduleViewModel : ViewModelBase
{
    private readonly SoldOutScheduler _scheduler;
    private int _resetHour = 9;
    private int _resetMinute;

    public SoldOutScheduleViewModel(SoldOutScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        var t = scheduler.DailyResetTime;
        _resetHour = t.Hours;
        _resetMinute = t.Minutes;
    }

    public int ResetHour
    {
        get => _resetHour;
        set
        {
            if (SetProperty(ref _resetHour, Math.Clamp(value, 0, 23)))
                Apply();
        }
    }

    public int ResetMinute
    {
        get => _resetMinute;
        set
        {
            if (SetProperty(ref _resetMinute, Math.Clamp(value, 0, 59)))
                Apply();
        }
    }

    /// <summary>说人话："每天 09:00 恢复供应"</summary>
    public string HumanText =>
        $"每天 {_resetHour:D2}:{_resetMinute:D2} 恢复供应";

    /// <summary>平台同步状态（未接入的置灰）</summary>
    public List<PlatformSyncStatus> Platforms { get; } = new();

    public void SetPlatforms(IEnumerable<ISoldOutSync> syncs)
    {
        Platforms.Clear();
        foreach (var s in syncs)
        {
            Platforms.Add(new PlatformSyncStatus
            {
                PlatformId = s.PlatformId,
                DisplayName = GetDisplayName(s.PlatformId),
                IsConnected = s.IsConnected,
            });
        }
        RaisePropertyChanged(nameof(Platforms));
    }

    private void Apply()
    {
        _scheduler.DailyResetTime = new TimeSpan(_resetHour, _resetMinute, 0);
        RaisePropertyChanged(nameof(HumanText));
    }

    private static string GetDisplayName(string platformId) => platformId switch
    {
        "self_platform" => "自有平台",
        "meituan" => "美团",
        "eleme" => "饿了么",
        "douyin" => "抖音",
        _ => platformId,
    };
}

/// <summary>平台同步状态（UI 显示用）</summary>
public sealed class PlatformSyncStatus
{
    public string PlatformId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string StatusText => IsConnected ? "已接入" : "未接入";
}
