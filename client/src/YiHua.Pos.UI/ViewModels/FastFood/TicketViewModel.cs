using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>单个取号的展示 ViewModel（用于队列列表绑定）</summary>
public sealed class TicketViewModel : ViewModelBase
{
    private readonly Ticket _ticket;

    public TicketViewModel(Ticket ticket)
    {
        _ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
    }

    public string Number => _ticket.Number;
    public int GuestCount => _ticket.GuestCount;
    public TicketStatus Status => _ticket.Status;
    public string IssueTimeText => _ticket.IssueTime.ToString("HH:mm");

    public string StatusText => _ticket.Status switch
    {
        TicketStatus.Waiting => "等待中",
        TicketStatus.Called => "叫号中",
        TicketStatus.Done => "已完成",
        TicketStatus.Skipped => "已跳号",
        TicketStatus.Voided => "已退号",
        _ => _ticket.Status.ToString(),
    };

    public void Refresh() => RaisePropertyChanged(string.Empty);
}
