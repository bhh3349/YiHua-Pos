using System.Collections.ObjectModel;
using YiHua.Pos.UI.Services.GroupBuy;

namespace YiHua.Pos.UI.ViewModels.GroupBuy;

/// <summary>核销记录项</summary>
public class RedeemRecordItem
{
    public string OrderNo { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Reversed { get; set; }
}

/// <summary>核销流水 ViewModel：按订单/券码/平台查询，含撤销记录</summary>
public class GroupBuyRecordViewModel : ViewModelBase
{
    private readonly IGroupBuyClient _client;
    public long StoreId { get; }

    public ObservableCollection<RedeemRecordItem> Records { get; } = new();

    private string _query = string.Empty;
    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    private string? _platformFilter;
    public string? PlatformFilter
    {
        get => _platformFilter;
        set { SetProperty(ref _platformFilter, value); ApplyFilter(); }
    }

    private string? _error;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public GroupBuyRecordViewModel(long storeId, IGroupBuyClient? client = null)
    {
        StoreId = storeId;
        _client = client ?? new FakeGroupBuyClient();
    }

    public void SetRecords(IEnumerable<RedeemRecordItem> records)
    {
        Records.Clear();
        foreach (var r in records)
            Records.Add(r);
    }

    private void ApplyFilter()
    {
        // 由调用方重新加载；此处仅触发刷新通知
        RaisePropertyChanged(nameof(Records));
    }

    /// <summary>撤销核销（需填原因，落流水）</summary>
    public async Task<bool> ReverseAsync(RedeemRecordItem record, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Error = "撤销原因必填";
            return false;
        }
        bool ok = await _client.ReverseAsync("", record.CouponCode, StoreId, reason, ct).ConfigureAwait(false);
        if (ok)
            record.Reversed = true;
        else
            Error = "撤销失败，请重试";
        return ok;
    }
}
