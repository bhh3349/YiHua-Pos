using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.FastFood;

/// <summary>就餐方式选择 ViewModel</summary>
public sealed class TakeawayViewModel : ViewModelBase
{
    private OrderType _orderType = OrderType.DineIn;
    private int _tablewareCount;
    private string _remark = string.Empty;

    /// <summary>堂食 / 外带 / 配送</summary>
    public OrderType OrderType
    {
        get => _orderType;
        set
        {
            if (SetProperty(ref _orderType, value))
            {
                RaisePropertyChanged(nameof(IsDineIn));
                RaisePropertyChanged(nameof(IsTakeAway));
                RaisePropertyChanged(nameof(IsDelivery));
                RaisePropertyChanged(nameof(NeedsTableware));
            }
        }
    }

    public bool IsDineIn => OrderType == OrderType.DineIn;
    public bool IsTakeAway => OrderType == OrderType.TakeAway;
    public bool IsDelivery => OrderType == OrderType.Delivery;

    /// <summary>是否需要餐具数量（外带/配送）</summary>
    public bool NeedsTableware => IsTakeAway || IsDelivery;

    /// <summary>餐具数量（几份餐具）</summary>
    public int TablewareCount
    {
        get => _tablewareCount;
        set => SetProperty(ref _tablewareCount, Math.Max(0, value));
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value ?? string.Empty);
    }

    public string OrderTypeText => OrderType switch
    {
        OrderType.DineIn => "堂食",
        OrderType.TakeAway => "外带",
        OrderType.Delivery => "配送",
        _ => OrderType.ToString(),
    };
}

/// <summary>配送信息 ViewModel（地址电话，团购平台对接用）</summary>
public sealed class DeliveryViewModel : ViewModelBase
{
    private string _address = string.Empty;
    private string _phone = string.Empty;
    private string _remark = string.Empty;

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value ?? string.Empty);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value ?? string.Empty);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value ?? string.Empty);
    }

    /// <summary>配送信息是否完整（地址+电话必填）</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(Phone);
}
