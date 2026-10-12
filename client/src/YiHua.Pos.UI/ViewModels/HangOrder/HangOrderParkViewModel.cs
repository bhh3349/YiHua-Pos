using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

// ViewModels.Desk 命名空间已存在（T-306），用别名避免 CS0118
using DDesk = YiHua.Pos.Domain.Entities.Desk;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>挂单确认 ViewModel</summary>
public sealed class HangOrderParkViewModel : ViewModelBase
{
    private readonly IHangOrderRepository _repo;
    private readonly IDeskRepository _deskRepo;
    private readonly HangOrderPrintService _printService;

    private string _remark = string.Empty;
    private string _operatorName = string.Empty;
    private bool _isParking;
    private string? _errorMessage;

    public HangOrderParkViewModel(
        IHangOrderRepository repo,
        IDeskRepository deskRepo,
        HangOrderPrintService printService)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _deskRepo = deskRepo ?? throw new ArgumentNullException(nameof(deskRepo));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
    }

    /// <summary>要挂单的订单</summary>
    public Order? Order { get; set; }

    /// <summary>桌台</summary>
    public DDesk? Desk { get; set; }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value ?? string.Empty);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value ?? string.Empty);
    }

    public bool IsParking
    {
        get => _isParking;
        private set => SetProperty(ref _isParking, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// 执行挂单：
    /// 1. 创建挂单记录（独立存储）
    /// 2. 桌台变 Idle（可开新单）——与挂单记录独立
    /// 3. 打印挂单通知
    /// </summary>
    public async Task<HangOrderRecord?> ParkAsync(CancellationToken ct = default)
    {
        if (Order == null || Desk == null)
        {
            ErrorMessage = "订单或桌台为空";
            return null;
        }

        IsParking = true;
        ErrorMessage = null;
        try
        {
            var record = new HangOrderRecord
            {
                DeskId = Desk.Id,
                DeskName = Desk.Name,
                OrderId = Order.Id,
                GuestCount = Order.GuestCount,
                Amount = Order.AmountPay,
                ParkTime = DateTime.Now,
                OperatorName = OperatorName,
                Remark = Remark,
            };

            await _repo.ParkAsync(record, ct).ConfigureAwait(false);

            // 关键：桌台变空闲（可开新单），挂单记录独立保留
            Desk.Status = DeskStatus.Idle;
            Desk.CurrentOrderId = 0;
            await _deskRepo.UpdateAsync(Desk, ct).ConfigureAwait(false);

            await _printService.PrintParkNoticeAsync(record, ct).ConfigureAwait(false);

            return record;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"挂单失败：{ex.Message}";
            return null;
        }
        finally
        {
            IsParking = false;
        }
    }
}
