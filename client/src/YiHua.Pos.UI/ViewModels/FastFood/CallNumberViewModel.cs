using YiHua.Pos.UI.Services.FastFood;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>
/// 叫号页 ViewModel（参考端 C000100063/64：叫号 / 取餐 / 再次叫号 / 历史订单）。
///
/// 叫号屏是给客人看的，所以：
///   · 当前叫号必须最大最显眼（客人在嘈杂环境里一眼要找得到）
///   · 等待队列要能按号找到人（点一下直接叫）
///   · 「再次叫号」要有 —— 客人没听见是常态，不是异常
/// </summary>
public class CallNumberViewModel
{
    private readonly ITicketQueue _queue;

    public CallNumberViewModel(ITicketQueue queue)
    {
        _queue = queue;
    }

    public string CurrentNumber { get; private set; } = "—";
    public string CurrentStatusText { get; private set; } = "还没有叫号";
    public List<TicketRowDto> Waiting { get; private set; } = new();
    public List<TicketRowDto> Called { get; private set; } = new();

    public int IssuedToday { get; private set; }
    public int WaitingCount { get; private set; }

    public string? ErrorMessage { get; private set; }
    public string? InfoMessage { get; private set; }

    /// <summary>制作中：待取餐的号数（对应收银台顶栏的「制作中 N 单」）。</summary>
    public int MakingCount => Called.Count;

    public void Refresh()
    {
        var current = _queue.GetCurrent();
        if (current != null)
        {
            CurrentNumber = current.Number;
            CurrentStatusText = current.Status switch
            {
                TicketStatus.Called => $"已叫号 · 等待取餐 · {current.GuestCount} 人",
                TicketStatus.Done => $"已取餐 · {current.GuestCount} 人",
                TicketStatus.Skipped => $"已跳号 · {current.GuestCount} 人",
                _ => current.GuestCount > 0 ? $"{current.GuestCount} 人" : "等待中",
            };
        }
        else
        {
            CurrentNumber = "—";
            CurrentStatusText = "还没有叫号";
        }

        Waiting = _queue.GetWaiting().Select(ToRow).ToList();
        IssuedToday = _queue.IssuedToday;
        WaitingCount = _queue.WaitingCount;

        // 「已叫未取」= 叫过号但还没来拿的（就是收银台顶栏说的「制作中」）。
        // 不能用 GetWaiting —— 叫过号就不再是 Waiting 了。
        Called = _queue.GetCalled().Select(ToRow).ToList();

        ErrorMessage = null;
    }

    /// <summary>发号（快餐模式下单时用）。</summary>
    public Ticket Issue(int guestCount = 1)
    {
        var ticket = _queue.Issue(guestCount);
        InfoMessage = $"已发号 {ticket.Number}";
        Refresh();
        return ticket;
    }

    /// <summary>叫下一个等待中的号。</summary>
    public void CallNext()
    {
        var ticket = _queue.CallNext();
        if (ticket == null)
        {
            ErrorMessage = "没有等待中的号了";
            return;
        }

        InfoMessage = $"叫号 {ticket.Number}";
        Refresh();
    }

    /// <summary>指定叫号（客人拿着号来问，或队列里点一下）。</summary>
    public void CallSpecific(string number)
    {
        var ticket = _queue.CallSpecific(number);
        if (ticket == null)
        {
            ErrorMessage = $"号 {number} 不在等待队列里";
            return;
        }

        InfoMessage = $"叫号 {number}";
        Refresh();
    }

    /// <summary>再次叫号（客人没听见是常态）。</summary>
    public void CallAgain()
    {
        var current = _queue.GetCurrent();
        if (current == null)
        {
            ErrorMessage = "还没有叫过号";
            return;
        }

        _queue.CallSpecific(current.Number);
        InfoMessage = $"再次叫号 {current.Number}";
        Refresh();
    }

    /// <summary>取餐完成。</summary>
    public void Complete(string number)
    {
        if (_queue.Complete(number))
        {
            InfoMessage = $"{number} 已取餐";
            Refresh();
        }
        else ErrorMessage = $"号 {number} 不在队列里";
    }

    /// <summary>跳号（叫了没人应，挪到队尾）。</summary>
    public void Skip(string number)
    {
        if (_queue.Skip(number))
        {
            InfoMessage = $"{number} 已跳号";
            Refresh();
        }
        else ErrorMessage = $"号 {number} 不在队列里";
    }

    /// <summary>优先（老人小孩、赶时间的客人）。</summary>
    public void Prioritize(string number)
    {
        if (_queue.Prioritize(number))
        {
            InfoMessage = $"{number} 已提前";
            Refresh();
        }
        else ErrorMessage = $"号 {number} 不在队列里";
    }

    /// <summary>作废（客人走了）。</summary>
    public void Void(string number)
    {
        if (_queue.Void(number))
        {
            InfoMessage = $"{number} 已作废";
            Refresh();
        }
        else ErrorMessage = $"号 {number} 不在队列里";
    }

    private static TicketRowDto ToRow(Ticket t) => new()
    {
        Number = t.Number,
        GuestCount = t.GuestCount,
        Status = t.Status,
        StatusText = t.Status switch
        {
            TicketStatus.Waiting => "等待中",
            TicketStatus.Called => "已叫号",
            TicketStatus.Done => "已取餐",
            TicketStatus.Skipped => "已跳号",
            TicketStatus.Voided => "已作废",
            TicketStatus.Expired => "已过期",
            _ => "—",
        },
        WaitedText = FormatWaited(DateTime.Now - t.IssueTime),
    };

    /// <summary>等待时长文案（叫号员要能看出谁等久了）。</summary>
    public static string FormatWaited(TimeSpan waited)
    {
        if (waited.TotalMinutes < 1) return "刚刚";
        if (waited.TotalMinutes < 60) return $"{(int)waited.TotalMinutes} 分钟";

        var hours = (int)waited.TotalHours;
        var minutes = waited.Minutes;
        return minutes == 0 ? $"{hours} 小时" : $"{hours} 小时 {minutes} 分";
    }
}

/// <summary>叫号列表行。</summary>
public class TicketRowDto
{
    public string Number { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public TicketStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string WaitedText { get; set; } = string.Empty;
}