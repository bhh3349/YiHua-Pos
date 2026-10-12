namespace YiHua.Pos.UI.Services.Ordering;

/// <summary>
/// 挂单快照（一桌的临时单）。
/// </summary>
public sealed class HangOrderSnapshot
{
    public string HangId { get; set; } = Guid.NewGuid().ToString("N");
    public long DeskId { get; set; }
    public string DeskName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public DateTime HangTime { get; set; } = DateTime.Now;
    public List<OrderLineInput> Lines { get; set; } = new();
    public string Remark { get; set; } = string.Empty;

    /// <summary>挂单金额（按当前行计算，不含整单折扣/抹零）</summary>
    public decimal Amount => OrderCalculator.CalcTotals(Lines).Payable;

    /// <summary>是否超过 30 分钟（可能已遗忘）</summary>
    public bool IsStale => (DateTime.Now - HangTime).TotalMinutes > 30;
}

/// <summary>
/// 挂单存储（内存实现）。
/// 挂单与正式订单数据上完全隔离：只存快照，不写订单表，结账时只认正式订单。
/// </summary>
public sealed class HangOrderStore
{
    private readonly Dictionary<string, HangOrderSnapshot> _hangs = new();
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _hangs.Count; }
    }

    /// <summary>挂单，返回挂单 ID</summary>
    public string Hang(HangOrderSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        lock (_lock)
        {
            _hangs[snapshot.HangId] = snapshot;
            return snapshot.HangId;
        }
    }

    /// <summary>取单（取出后从挂单列表移除）</summary>
    public HangOrderSnapshot? Take(string hangId)
    {
        lock (_lock)
        {
            if (_hangs.TryGetValue(hangId, out var s))
            {
                _hangs.Remove(hangId);
                return s;
            }
            return null;
        }
    }

    /// <summary>查看挂单（不移除）</summary>
    public HangOrderSnapshot? Peek(string hangId)
    {
        lock (_lock)
        {
            _hangs.TryGetValue(hangId, out var s);
            return s;
        }
    }

    /// <summary>挂单列表（按挂单时间倒序）</summary>
    public IReadOnlyList<HangOrderSnapshot> List()
    {
        lock (_lock)
        {
            return _hangs.Values.OrderByDescending(h => h.HangTime).ToList();
        }
    }

    /// <summary>删除挂单</summary>
    public bool Remove(string hangId)
    {
        lock (_lock) return _hangs.Remove(hangId);
    }
}
