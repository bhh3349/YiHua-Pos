using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.ViewModels.Desk;

/// <summary>预订信息（Domain 暂无预订实体，此处为 UI 层模型；见 Domain 缺口清单）</summary>
public class ReservationInfo
{
    public long DeskId { get; set; }
    public string DeskName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime ReserveTime { get; set; }
    public int GuestCount { get; set; }

    /// <summary>预订状态：Upcoming=快到了（30分钟内）/ Overdue=已过15分钟 / Normal</summary>
    public string GetReminderState(DateTime now)
    {
        var diff = ReserveTime - now;
        if (diff.TotalMinutes < -15)
            return "Overdue";
        if (diff.TotalMinutes <= 30)
            return "Upcoming";
        return "Normal";
    }
}

/// <summary>桌台卡片展示模型</summary>
public class DeskCardModel : ViewModelBase
{
    private string _elapsedText = string.Empty;

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public DeskStatus Status { get; set; }
    public int GuestCount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? OpenTime { get; set; }

    public string ElapsedText
    {
        get => _elapsedText;
        set => SetProperty(ref _elapsedText, value);
    }

    public static string FormatElapsed(DateTime? openTime, DateTime now)
    {
        if (openTime == null)
            return string.Empty;
        var span = now - openTime.Value;
        if (span.TotalMinutes < 1)
            return "刚开台";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes}分钟";
        return $"{(int)span.TotalHours}小时{(int)(span.TotalMinutes % 60)}分";
    }
}
