using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>订单主表</summary>
public class Order
{
    public long Id { get; set; }

    /// <summary>订单号（服务端生成）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户端幂等键（本地生成 UUID，用于断网补传去重）</summary>
    public string ClientOrderNo { get; set; } = string.Empty;

    /// <summary>桌台 ID（快餐为 0）</summary>
    public long DeskId { get; set; }

    public OrderType OrderType { get; set; } = OrderType.DineIn;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>就餐人数</summary>
    public int GuestCount { get; set; }

    /// <summary>商品总额</summary>
    public decimal AmountTotal { get; set; }

    /// <summary>折扣总额</summary>
    public decimal AmountDiscount { get; set; }

    /// <summary>应付金额</summary>
    public decimal AmountPay { get; set; }

    /// <summary>已退金额</summary>
    public decimal AmountRefund { get; set; }

    /// <summary>实收金额</summary>
    public decimal AmountReceived { get; set; }

    /// <summary>找零</summary>
    public decimal AmountChange { get; set; }

    /// <summary>取餐号（快餐）</summary>
    public string TakeNo { get; set; } = string.Empty;

    public long ShiftId { get; set; }

    public long OperatorId { get; set; }

    public string Remark { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? SettleTime { get; set; }

    /// <summary>同步状态（本地字段，不上传）</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

/// <summary>订单明细</summary>
public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    /// <summary>规格/做法文本，如「微辣」</summary>
    public string SpecText { get; set; } = string.Empty;

    /// <summary>加料（JSON 或逗号分隔）</summary>
    public string ExtraText { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>数量（称重商品为小数）</summary>
    public decimal Qty { get; set; } = 1;

    /// <summary>小计 = Price * Qty</summary>
    public decimal Amount { get; set; }

    /// <summary>本行折扣额</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>是否赠品（不计价）</summary>
    public bool IsGift { get; set; }

    /// <summary>是否称重商品</summary>
    public bool IsWeighted { get; set; }

    public string Remark { get; set; } = string.Empty;

    /// <summary>是否已退</summary>
    public bool IsRefunded { get; set; }
}

/// <summary>支付记录</summary>
public class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    public PayType PayType { get; set; }

    /// <summary>自定义收款方式的名称（商家自己填，如「微信个人码」）</summary>
    public string PayTypeName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime PayTime { get; set; } = DateTime.Now;

    public long OperatorId { get; set; }
}
