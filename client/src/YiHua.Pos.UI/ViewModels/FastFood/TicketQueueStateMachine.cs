using System.Collections.ObjectModel;
using YiHua.Pos.UI.Services.FastFood;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>
/// 取号状态机（UI 编排层，纯逻辑）。
/// 封装 ITicketQueue，对外暴露可绑定的叫号状态。
/// </summary>
public sealed class TicketQueueStateMachine : ViewModelBase
{
    private readonly ITicketQueue _queue;
    private Ticket? _current;

    public TicketQueueStateMachine(ITicketQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Refresh();
    }

    /// <summary>当前叫号（如 "A051"），无则为空</summary>
    public string CurrentNumber => _current?.Number ?? string.Empty;

    /// <summary>当前叫号状态文本</summary>
    public string CurrentStatusText => _current switch
    {
        null => "暂未叫号",
        { Status: TicketStatus.Called } => "正在点单",
        { Status: TicketStatus.Done } => "已完成",
        _ => _current.Status.ToString(),
    };

    /// <summary>等待中的号</summary>
    public ObservableCollection<string> WaitingNumbers { get; } = new();

    /// <summary>等待人数</summary>
    public int WaitingCount => WaitingNumbers.Count;

    /// <summary>今日已发号数</summary>
    public int IssuedToday => _queue.IssuedToday;

    /// <summary>是否有等待中的号</summary>
    public bool HasWaiting => WaitingCount > 0;

    public event EventHandler<Ticket?>? TicketCalled;

    /// <summary>发号</summary>
    public Ticket Issue(int guestCount = 1)
    {
        var t = _queue.Issue(guestCount);
        Refresh();
        return t;
    }

    /// <summary>叫下一个</summary>
    public Ticket? CallNext()
    {
        var t = _queue.CallNext();
        if (t != null)
        {
            _current = t;
            TicketCalled?.Invoke(this, t);
        }
        Refresh();
        return t;
    }

    /// <summary>指定叫号</summary>
    public Ticket? CallSpecific(string number)
    {
        var t = _queue.CallSpecific(number);
        if (t != null)
        {
            _current = t;
            TicketCalled?.Invoke(this, t);
        }
        Refresh();
        return t;
    }

    /// <summary>跳号</summary>
    public bool Skip(string number)
    {
        bool ok = _queue.Skip(number);
        if (ok && _current?.Number == number)
            _current = _queue.GetCurrent();
        Refresh();
        return ok;
    }

    /// <summary>插队</summary>
    public bool Prioritize(string number)
    {
        bool ok = _queue.Prioritize(number);
        Refresh();
        return ok;
    }

    /// <summary>退号</summary>
    public bool Void(string number)
    {
        bool ok = _queue.Void(number);
        Refresh();
        return ok;
    }

    /// <summary>完成当前号</summary>
    public bool CompleteCurrent()
    {
        if (_current == null) return false;
        bool ok = _queue.Complete(_current.Number);
        if (ok) _current = null;
        Refresh();
        return ok;
    }

    private void Refresh()
    {
        WaitingNumbers.Clear();
        foreach (var t in _queue.GetWaiting())
            WaitingNumbers.Add(t.Number);

        RaisePropertyChanged(nameof(CurrentNumber));
        RaisePropertyChanged(nameof(CurrentStatusText));
        RaisePropertyChanged(nameof(WaitingCount));
        RaisePropertyChanged(nameof(IssuedToday));
        RaisePropertyChanged(nameof(HasWaiting));
    }
}
