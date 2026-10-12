using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.HangOrder;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.HangOrder;

/// <summary>拆单/预结账 ViewModel</summary>
public sealed class SplitOrderViewModel : ViewModelBase
{
    private readonly IHangOrderRepository _repo;
    private readonly IOrderRepository _orderRepo;
    private SplitStateMachine? _splitter;
    private string? _errorMessage;

    public SplitOrderViewModel(
        IHangOrderRepository repo,
        IOrderRepository orderRepo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
    }

    public HangOrderRecord? Record { get; private set; }
    public Order? Order { get; private set; }

    public SplitStateMachine? Splitter
    {
        get => _splitter;
        private set => SetProperty(ref _splitter, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public async Task LoadAsync(string recordId, CancellationToken ct = default)
    {
        ErrorMessage = null;
        try
        {
            Record = await _repo.GetByIdAsync(recordId, ct).ConfigureAwait(false);
            if (Record == null)
            {
                ErrorMessage = "挂单记录不存在";
                return;
            }

            Order = await _orderRepo.GetByIdAsync(Record.OrderId, ct).ConfigureAwait(false);
            if (Order == null)
            {
                ErrorMessage = "原订单不存在";
                return;
            }

            Splitter = new SplitStateMachine(Order, Record.SettledItemIds);
            RaisePropertyChanged(nameof(Record));
            RaisePropertyChanged(nameof(Order));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 确认预结账：标记选中明细为已结，更新订单状态。
    /// 多次预结账累加：SettledItemIds 持续追加。
    /// </summary>
    public async Task<bool> ConfirmSplitAsync(CancellationToken ct = default)
    {
        if (Record == null || Order == null || Splitter == null)
        {
            ErrorMessage = "数据未加载";
            return false;
        }

        var ids = Splitter.Confirm(out string? reason);
        if (ids.Count == 0)
        {
            ErrorMessage = reason ?? "拆单校验失败";
            return false;
        }

        try
        {
            foreach (var id in ids)
                if (!Record.SettledItemIds.Contains(id))
                    Record.SettledItemIds.Add(id);

            // 更新订单状态
            Order.Status = HangOrderCalculator.CalcStatusAfterSplit(Order, Record.SettledItemIds);
            await _orderRepo.UpdateAsync(Order, ct).ConfigureAwait(false);
            await _repo.UpdateAsync(Record, ct).ConfigureAwait(false);

            // 全部结完 → 订单 Settled，挂单记录可清理
            // 部分结 → 订单 Pending，挂单继续保留
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"预结账失败：{ex.Message}";
            return false;
        }
    }
}
