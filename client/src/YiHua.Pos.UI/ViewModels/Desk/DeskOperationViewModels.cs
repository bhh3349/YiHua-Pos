using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using DeskEntity = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.ViewModels.Desk;

/// <summary>开台 ViewModel：选空闲桌 → 填人数 → 确认（预订桌自动带出人数）</summary>
public class DeskOpenViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;

    private int _guestCount = 2;
    private string _errorMessage = string.Empty;

    public DeskOpenViewModel(IDeskRepository deskRepository, DeskEntity desk, ReservationInfo? reservation = null)
    {
        _deskRepository = deskRepository;
        Desk = desk;
        if (reservation != null)
            _guestCount = reservation.GuestCount;
    }

    public DeskEntity Desk { get; }

    /// <summary>就餐人数（NumberPad 输入）</summary>
    public int GuestCount
    {
        get => _guestCount;
        set => SetProperty(ref _guestCount, Math.Max(1, value));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler? Opened;

    /// <summary>确认开台：仅开台成功时发事件，由调用方跳转点单界面</summary>
    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        var check = DeskStatusTransitions.TryOpen(Desk);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        Desk.Status = DeskStatus.Occupied;
        Desk.GuestCount = GuestCount;
        Desk.OpenTime = DateTime.Now;
        await _deskRepository.UpdateAsync(Desk, ct).ConfigureAwait(false);

        Opened?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>换台 ViewModel：源桌（占用）→ 目标桌（空闲），明细搬移，人数继承</summary>
public class DeskChangeViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;
    private DeskEntity? _targetDesk;
    private string _errorMessage = string.Empty;

    public DeskChangeViewModel(IDeskRepository deskRepository, DeskEntity sourceDesk)
    {
        _deskRepository = deskRepository;
        SourceDesk = sourceDesk;
    }

    public DeskEntity SourceDesk { get; }

    public DeskEntity? TargetDesk
    {
        get => _targetDesk;
        set => SetProperty(ref _targetDesk, value);
    }

    /// <summary>可选的目标桌（空闲桌，由调用方设置）</summary>
    public List<DeskEntity> AvailableTargets { get; set; } = new();

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler? Changed;

    /// <summary>
    /// 确认换台（需二次确认）：
    /// 目标桌继承源桌人数与订单，源桌变空闲。
    /// </summary>
    public async Task<bool> ConfirmAsync(bool sourceHasUnsettledItems, CancellationToken ct = default)
    {
        if (TargetDesk == null)
        {
            ErrorMessage = "请选择目标桌台";
            return false;
        }

        var check = DeskStatusTransitions.TryChange(SourceDesk, TargetDesk, sourceHasUnsettledItems);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        // 明细搬移：目标桌继承订单与人数
        TargetDesk.Status = DeskStatus.Occupied;
        TargetDesk.GuestCount = SourceDesk.GuestCount;
        TargetDesk.CurrentOrderId = SourceDesk.CurrentOrderId;
        TargetDesk.OpenTime = SourceDesk.OpenTime;
        TargetDesk.CurrentAmount = SourceDesk.CurrentAmount;

        SourceDesk.Status = DeskStatus.Idle;
        SourceDesk.GuestCount = 0;
        SourceDesk.CurrentOrderId = 0;
        SourceDesk.OpenTime = null;
        SourceDesk.CurrentAmount = 0;

        await _deskRepository.UpdateAsync(TargetDesk, ct).ConfigureAwait(false);
        await _deskRepository.UpdateAsync(SourceDesk, ct).ConfigureAwait(false);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>并台 ViewModel：两桌占用 → 明细合并到一桌，人数相加，另一桌空闲</summary>
public class DeskMergeViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;
    private DeskEntity? _targetDesk;
    private string _errorMessage = string.Empty;

    public DeskMergeViewModel(IDeskRepository deskRepository, DeskEntity sourceDesk)
    {
        _deskRepository = deskRepository;
        SourceDesk = sourceDesk;
    }

    public DeskEntity SourceDesk { get; }

    /// <summary>合并到的目标桌（保留桌）</summary>
    public DeskEntity? TargetDesk
    {
        get => _targetDesk;
        set => SetProperty(ref _targetDesk, value);
    }

    /// <summary>可选的目标桌（占用桌，由调用方设置）</summary>
    public List<DeskEntity> AvailableTargets { get; set; } = new();

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler? Merged;

    /// <summary>确认并台（需二次确认）：人数相加，金额相加</summary>
    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        if (TargetDesk == null)
        {
            ErrorMessage = "请选择要合并的桌台";
            return false;
        }

        var check = DeskStatusTransitions.TryMerge(SourceDesk, TargetDesk);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        TargetDesk.GuestCount += SourceDesk.GuestCount;
        TargetDesk.CurrentAmount += SourceDesk.CurrentAmount;
        // 注：订单明细合并需订单模块支持，此处合并桌台级汇总；见 Domain 缺口清单

        SourceDesk.Status = DeskStatus.Idle;
        SourceDesk.GuestCount = 0;
        SourceDesk.CurrentOrderId = 0;
        SourceDesk.OpenTime = null;
        SourceDesk.CurrentAmount = 0;

        await _deskRepository.UpdateAsync(TargetDesk, ct).ConfigureAwait(false);
        await _deskRepository.UpdateAsync(SourceDesk, ct).ConfigureAwait(false);

        Merged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>清台 ViewModel：必须已结账，清台后写记录</summary>
public class DeskClearViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;
    private string _errorMessage = string.Empty;

    public DeskClearViewModel(IDeskRepository deskRepository, DeskEntity desk, string operatorName)
    {
        _deskRepository = deskRepository;
        Desk = desk;
        OperatorName = operatorName;
    }

    public DeskEntity Desk { get; }
    public string OperatorName { get; }
    public DateTime ClearTime { get; } = DateTime.Now;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler? Cleared;

    public async Task<bool> ConfirmAsync(bool isSettled, CancellationToken ct = default)
    {
        var check = DeskStatusTransitions.TryClear(Desk, isSettled);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        Desk.Status = DeskStatus.Idle;
        Desk.GuestCount = 0;
        Desk.CurrentOrderId = 0;
        Desk.OpenTime = null;
        Desk.CurrentAmount = 0;
        await _deskRepository.UpdateAsync(Desk, ct).ConfigureAwait(false);

        // 清台记录：谁清的、几点（写入备注；正式审计表见 Domain 缺口清单）
        Cleared?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>预订 ViewModel：桌号 + 时间 + 人数 + 客人名 + 电话（可选）</summary>
public class DeskReserveViewModel : ViewModelBase
{
    private readonly IDeskRepository _deskRepository;

    private string _customerName = string.Empty;
    private string? _phone;
    private DateTime _reserveTime = DateTime.Now.AddHours(1);
    private int _guestCount = 2;
    private string _errorMessage = string.Empty;

    public DeskReserveViewModel(IDeskRepository deskRepository, DeskEntity desk)
    {
        _deskRepository = deskRepository;
        Desk = desk;
    }

    public DeskEntity Desk { get; }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string? Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public DateTime ReserveTime
    {
        get => _reserveTime;
        set => SetProperty(ref _reserveTime, value);
    }

    public int GuestCount
    {
        get => _guestCount;
        set => SetProperty(ref _guestCount, Math.Max(1, value));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public event EventHandler<ReservationInfo>? Reserved;

    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ErrorMessage = "请填写客人姓名";
            return false;
        }
        if (ReserveTime <= DateTime.Now)
        {
            ErrorMessage = "预订时间必须晚于现在";
            return false;
        }

        var check = DeskStatusTransitions.TryReserve(Desk);
        if (!check.Ok)
        {
            ErrorMessage = check.Error;
            return false;
        }

        Desk.Status = DeskStatus.Reserved;
        await _deskRepository.UpdateAsync(Desk, ct).ConfigureAwait(false);

        var info = new ReservationInfo
        {
            DeskId = Desk.Id,
            DeskName = Desk.Name,
            CustomerName = CustomerName.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            ReserveTime = ReserveTime,
            GuestCount = GuestCount,
        };
        Reserved?.Invoke(this, info);
        return true;
    }
}
