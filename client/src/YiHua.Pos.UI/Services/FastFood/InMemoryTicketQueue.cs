namespace YiHua.Pos.UI.Services.FastFood;

/// <summary>
/// 取号队列内存实现。
/// 发号规则：前缀（默认 "A"）+ 当日序号（3 位，如 A052），日切后从 A001 重新开始。
/// 线程安全。
/// </summary>
public sealed class InMemoryTicketQueue : ITicketQueue
{
    private readonly object _lock = new();
    private readonly List<Ticket> _tickets = new();
    private readonly string _prefix;
    private DateTime _currentDate;
    private int _sequence;

    public InMemoryTicketQueue(string prefix = "A")
    {
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "A" : prefix.Trim();
        _currentDate = DateTime.Today;
        _sequence = 0;
    }

    public int IssuedToday
    {
        get { lock (_lock) { EnsureSameDay(); return _sequence; } }
    }

    public int WaitingCount
    {
        get { lock (_lock) return _tickets.Count(t => t.Status == TicketStatus.Waiting); }
    }

    public Ticket Issue(int guestCount = 1)
    {
        lock (_lock)
        {
            EnsureSameDay();
            _sequence++;
            var ticket = new Ticket
            {
                Number = FormatNumber(_sequence),
                GuestCount = Math.Max(1, guestCount),
                IssueTime = DateTime.Now,
            };
            _tickets.Add(ticket);
            return ticket;
        }
    }

    public Ticket? CallNext()
    {
        lock (_lock)
        {
            var next = _tickets.FirstOrDefault(t => t.Status == TicketStatus.Waiting);
            if (next == null) return null;
            return CallInternal(next);
        }
    }

    public Ticket? CallSpecific(string number)
    {
        lock (_lock)
        {
            var t = Find(number);
            if (t == null || t.Status != TicketStatus.Waiting) return null;
            return CallInternal(t);
        }
    }

    public bool Skip(string number)
    {
        lock (_lock)
        {
            var t = Find(number);
            if (t == null) return false;
            // 跳号：只有叫号中的才能跳，跳过后回到等待队尾
            if (t.Status != TicketStatus.Called) return false;
            t.Status = TicketStatus.Skipped;
            // 重新排队：生成一个新的等待记录（保留原号）
            var requeue = new Ticket
            {
                Number = t.Number,
                GuestCount = t.GuestCount,
                IssueTime = t.IssueTime,
                Status = TicketStatus.Waiting,
            };
            _tickets.Add(requeue);
            return true;
        }
    }

    public bool Prioritize(string number)
    {
        lock (_lock)
        {
            var t = Find(number);
            if (t == null || t.Status != TicketStatus.Waiting) return false;
            // 插队：移到等待队列最前面
            _tickets.Remove(t);
            int idx = _tickets.FindIndex(x => x.Status == TicketStatus.Waiting);
            if (idx < 0) _tickets.Add(t);
            else _tickets.Insert(idx, t);
            return true;
        }
    }

    public bool Void(string number)
    {
        lock (_lock)
        {
            var t = Find(number);
            if (t == null) return false;
            if (t.Status == TicketStatus.Done || t.Status == TicketStatus.Voided) return false;
            t.Status = TicketStatus.Voided;
            return true;
        }
    }

    public bool Complete(string number)
    {
        lock (_lock)
        {
            var t = Find(number);
            if (t == null) return false;
            if (t.Status != TicketStatus.Called) return false;
            t.Status = TicketStatus.Done;
            t.DoneTime = DateTime.Now;
            return true;
        }
    }

    public IReadOnlyList<Ticket> GetWaiting()
    {
        lock (_lock)
        {
            return _tickets.Where(t => t.Status == TicketStatus.Waiting).ToList();
        }
    }

    public Ticket? GetCurrent()
    {
        lock (_lock)
        {
            return _tickets.LastOrDefault(t => t.Status == TicketStatus.Called);
        }
    }

    private Ticket CallInternal(Ticket ticket)
    {
        ticket.Status = TicketStatus.Called;
        ticket.CallTime = DateTime.Now;
        return ticket;
    }

    private Ticket? Find(string number)
    {
        return _tickets.LastOrDefault(t =>
            t.Number.Equals(number, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TicketStatus.Voided &&
            t.Status != TicketStatus.Done);
    }

    private string FormatNumber(int seq) => $"{_prefix}{seq:D3}";

    /// <summary>日切检查：日期变了则序号重置</summary>
    private void EnsureSameDay()
    {
        var today = DateTime.Today;
        if (today != _currentDate)
        {
            _currentDate = today;
            _sequence = 0;
        }
    }
}
