namespace YiHua.Pos.Domain.Enums;

/// <summary>门店经营模式</summary>
public enum StoreMode
{
    /// <summary>正餐（中餐/火锅，有桌台）</summary>
    DineIn = 1,

    /// <summary>快餐（小吃/奶茶，取餐码）</summary>
    FastFood = 2,
}

/// <summary>订单状态</summary>
public enum OrderStatus
{
    /// <summary>待结账</summary>
    Pending = 0,

    /// <summary>已结账</summary>
    Settled = 1,

    /// <summary>已退单</summary>
    Refunded = 2,

    /// <summary>已作废</summary>
    Void = 3,
}

/// <summary>订单类型</summary>
public enum OrderType
{
    /// <summary>堂食</summary>
    DineIn = 1,

    /// <summary>外带</summary>
    TakeAway = 2,

    /// <summary>外卖</summary>
    Delivery = 3,

    /// <summary>自提</summary>
    PickUp = 4,
}

/// <summary>桌台状态</summary>
public enum DeskStatus
{
    /// <summary>空闲</summary>
    Idle = 0,

    /// <summary>占用（已开台）</summary>
    Occupied = 1,

    /// <summary>已预订</summary>
    Reserved = 2,
}

/// <summary>支付方式</summary>
public enum PayType
{
    /// <summary>现金</summary>
    Cash = 1,

    /// <summary>自定义收款（商家自己的收款码，仅记账）</summary>
    Custom = 2,

    /// <summary>会员余额（二期）</summary>
    Member = 3,

    // ---- 以下为预留，一期不实现 ----
    /// <summary>扫码支付（预留）</summary>
    QrCode = 90,

    /// <summary>银行卡（预留）</summary>
    Bank = 91,
}

/// <summary>交班状态</summary>
public enum ShiftStatus
{
    /// <summary>进行中</summary>
    Open = 0,

    /// <summary>已交班</summary>
    Closed = 1,
}

/// <summary>同步状态（outbox）</summary>
public enum SyncStatus
{
    /// <summary>待发送</summary>
    Pending = 0,

    /// <summary>已成功</summary>
    Succeeded = 1,

    /// <summary>已失败（超过重试次数）</summary>
    Failed = 2,
}

/// <summary>打印机连接方式</summary>
public enum PrinterConnection
{
    Usb,
    Serial,
    Network,
    Bluetooth,
}

/// <summary>打印模板类型</summary>
public enum TemplateType
{
    /// <summary>前台小票</summary>
    Receipt = 1,

    /// <summary>后厨单</summary>
    Kitchen = 2,

    /// <summary>标签</summary>
    Label = 3,

    /// <summary>交班单</summary>
    Handover = 4,
}

/// <summary>退款类型</summary>
public enum RefundType
{
    /// <summary>退菜（部分退）</summary>
    Item,

    /// <summary>整单退单</summary>
    Order,

    /// <summary>冲正（作废，仅未结账订单可用）</summary>
    Void,
}

/// <summary>挂单状态</summary>
public enum HangOrderStatus
{
    /// <summary>挂起中，等待取单</summary>
    Parked,

    /// <summary>已取回，订单恢复结账</summary>
    Recalled,

    /// <summary>已作废（批量清理或超时自动清理）</summary>
    Cancelled,
}