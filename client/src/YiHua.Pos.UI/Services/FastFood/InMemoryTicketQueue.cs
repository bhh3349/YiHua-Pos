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
    /// <summary>时钟注入：日切逻辑必须可测，否则跨零点这条路径永远测不到。</summary>
    private readonly Func<DateTime> _now;
    private DateTime _currentDate;
    private int _sequence;

    public InMemoryTicketQueue(string prefix = "A", Func<DateTime>? now = null)
    {
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "A" : prefix.Trim();
        _now = now ?? (() => DateTime.Now);
        _currentDate = _now().Date;
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
                IssueTime = _now(),
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
            t.DoneTime = _now();
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

    /// <summary>
    /// 已叫号但还没取餐的号（收银台顶栏的「制作中」）。
    /// 按叫号时间排序：先叫的客人等得久，取餐时也该先走。
    /// </summary>
    public IReadOnlyList<Ticket> GetCalled()
    {
        lock (_lock)
        {
            return _tickets
                .Where(t => t.Status == TicketStatus.Called)
                .OrderBy(t => t.CallTime ?? t.IssueTime)
                .ToList();
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
        ticket.CallTime = _now();
        return ticket;
    }

    private Ticket? Find(string number)
    {
        // 号码按日复用：昨天的 A001 和今天的 A001 同号。
        // 所以查找必须排除所有已终止状态的号（过期/ 退号 / 完成 / 跳号），
        // 否则过期号会和新号互相干扰 —— 叫号、退号可能作用到错的记录上。
        return _tickets.LastOrDefault(t =>
            t.Number.Equals(number, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TicketStatus.Voided &&
            t.Status != TicketStatus.Done &&
    t.Status != TicketStatus.Expired &&
            t.Status != TicketStatus.Skipped);
    }

    private string FormatNumber(int seq) => $"{_prefix}{seq:D3}";

    /// <summary>
    /// 日切检查：日期变了则序号重置。
    ///
    /// 同时把「昨天留下、还没叫到或没完成的号」标记为过期 —— 快餐店通宵营业时，
    /// 昨天的 A052 会一直挂在等待队列里；序号重置后新客人又从 A001 开始，
    /// 那个昨天的号就永远等不到叫号了。所以日切必须清理过期待叫的号。
    /// </summary>
    private void EnsureSameDay()
    {
        var today = _now().Date;
        if (today != _currentDate)
        {
            _currentDate = today;
            _sequence = 0;
            // 过期的号不能直接丢：叫号屏可能还显示着，客人会来问。
            // 标记为过期保留在列表里，GetWaiting 不再返回它们。
            foreach (var t in _tickets)
            {
                if (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Called)
                    t.Status = TicketStatus.Expired;
            }
        }
    }
}
