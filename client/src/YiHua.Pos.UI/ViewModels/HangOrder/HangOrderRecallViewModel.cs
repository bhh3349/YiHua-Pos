using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>取单 ViewModel（处理桌台冲突/换桌）</summary>
public sealed class HangOrderRecallViewModel : ViewModelBase
{
    private readonly IHangOrderRepository _repo;
    private readonly IDeskRepository _deskRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly HangOrderPrintService _printService;

    private string? _conflictMessage;
    private bool _isRecalling;
    private string? _errorMessage;

    public HangOrderRecallViewModel(
        IHangOrderRepository repo,
        IDeskRepository deskRepo,
        IOrderRepository orderRepo,
        HangOrderPrintService printService)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _deskRepo = deskRepo ?? throw new ArgumentNullException(nameof(deskRepo));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
    }

    public HangOrderRecord? Record { get; set; }

    /// <summary>冲突提示（桌台被占时）</summary>
    public string? ConflictMessage
    {
        get => _conflictMessage;
        private set => SetProperty(ref _conflictMessage, value);
    }

    public bool HasConflict => ConflictMessage != null;

    public bool IsRecalling
    {
        get => _isRecalling;
        private set => SetProperty(ref _isRecalling, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>检查原桌是否可恢复</summary>
    public async Task<bool> CheckOriginalDeskAsync(CancellationToken ct = default)
    {
        if (Record == null) return false;
        var desk = await _deskRepo.GetByIdAsync(Record.DeskId, ct).ConfigureAwait(false);
        if (desk == null)
        {
            ConflictMessage = "原桌台不存在，请选择其他桌台";
            RaisePropertyChanged(nameof(HasConflict));
            return false;
        }

        if (!HangOrderCalculator.CanRecall(Record, desk, out string? reason))
        {
            ConflictMessage = reason;
            RaisePropertyChanged(nameof(HasConflict));
            return false;
        }

        ConflictMessage = null;
        RaisePropertyChanged(nameof(HasConflict));
        return true;
    }

    /// <summary>
    /// 执行取单：恢复订单到指定桌台。
    /// </summary>
    public async Task<bool> RecallAsync(long deskId, CancellationToken ct = default)
    {
        if (Record == null)
        {
            ErrorMessage = "挂单记录为空";
            return false;
        }

        IsRecalling = true;
        ErrorMessage = null;
        try
        {
            var desk = await _deskRepo.GetByIdAsync(deskId, ct).ConfigureAwait(false);
            if (desk == null)
            {
                ErrorMessage = "桌台不存在";
                return false;
            }

            if (!HangOrderCalculator.CanRecall(Record, desk, out string? reason))
            {
                ConflictMessage = reason;
                RaisePropertyChanged(nameof(HasConflict));
                return false;
            }

            var order = await _orderRepo.GetByIdAsync(Record.OrderId, ct).ConfigureAwait(false);
            if (order == null)
            {
                ErrorMessage = "原订单不存在";
                return false;
            }

            // 恢复：订单回 Pending，桌台变占用
            order.Status = OrderStatus.Pending;
            order.DeskId = desk.Id;
            await _orderRepo.UpdateAsync(order, ct).ConfigureAwait(false);

            desk.Status = DeskStatus.Occupied;
            desk.CurrentOrderId = order.Id;
            await _deskRepo.UpdateAsync(desk, ct).ConfigureAwait(false);

            await _repo.RemoveAsync(Record.Id, ct).ConfigureAwait(false);
            await _printService.PrintRecallNoticeAsync(Record, desk.Name, ct).ConfigureAwait(false);

            ConflictMessage = null;
            RaisePropertyChanged(nameof(HasConflict));
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"取单失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsRecalling = false;
        }
    }
}
