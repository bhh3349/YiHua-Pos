namespace YiHua.Pos.UI.Services.FastFood;

/// <summary>取号状态</summary>
public enum TicketStatus
{
    /// <summary>等待叫号</summary>
    Waiting,
    /// <summary>已叫号（正在点单/取餐）</summary>
    Called,
    /// <summary>已完成</summary>
    Done,
    /// <summary>已跳号</summary>
    Skipped,
    /// <summary>已退号</summary>
    Voided,
    /// <summary>跨日过期：昨天没叫到也没完成的号。</summary>
    /// <summary>快餐店通宵营业时，日切后昨天的号不能再被叫到，但记录保留供查询。</summary>
    Expired,
}

/// <summary>取号记录</summary>
public sealed class Ticket
{
    /// <summary>取号，如 "A052"</summary>
    public string Number { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Waiting;

    /// <summary>就餐人数</summary>
    public int GuestCount { get; set; } = 1;

    public DateTime IssueTime { get; set; } = DateTime.Now;
    public DateTime? CallTime { get; set; }
    public DateTime? DoneTime { get; set; }
}

/// <summary>
/// 取号队列接口（纯内存 + 可注入，Domain 补上实体后可换持久化实现）。
/// </summary>
public interface ITicketQueue
{
    /// <summary>发号（按日序号递增）</summary>
    Ticket Issue(int guestCount = 1);

    /// <summary>叫下一个等待中的号</summary>
    Ticket? CallNext();

    /// <summary>指定叫号</summary>
    Ticket? CallSpecific(string number);

    /// <summary>跳号（跳过当前叫号，移到队尾之后）</summary>
    bool Skip(string number);

    /// <summary>插队（把指定号移到等待队列最前面）</summary>
    bool Prioritize(string number);

    /// <summary>退号</summary>
    bool Void(string number);

    /// <summary>完成（取餐完毕）</summary>
    bool Complete(string number);

    /// <summary>等待中的号（按顺序）</summary>
    IReadOnlyList<Ticket> GetWaiting();

    /// <summary>
    /// 已叫号但还没取餐的号。
    /// 这批就是收银台顶栏说的「制作中」—— 饭做好了、叫了号、客人还没来拿。
    /// GetWaiting 拿不到它们：叫过号就不再是 Waiting 了。
    /// </summary>
    IReadOnlyList<Ticket> GetCalled();

    /// <summary>当前叫号中的号</summary>
    Ticket? GetCurrent();

    /// <summary>今日已发号总数</summary>
    int IssuedToday { get; }

    /// <summary>等待人数</summary>
    int WaitingCount { get; }
}
