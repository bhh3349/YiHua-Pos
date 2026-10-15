namespace YiHua.Pos.Domain.Entities;

/// <summary>
/// 会员。
///
/// 单店模式：会员只在本店有效，卡号本店唯一。
/// 余额和积分都记流水（见 MemberTransaction），
/// 光看余额看不出「这 200 块是怎么来的」，对不了账。
/// </summary>
public class Member
{
    public long Id { get; set; }

    /// <summary>卡号（本店唯一，可手输或自动生成）。</summary>
    public string CardNo { get; set; } = string.Empty;

    /// <summary>手机号（可空；有的话可以用来查会员）。</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>会员等级名（如「普通」「银卡」「金卡」，自由文本，不做等级表）。</summary>
    public string Level { get; set; } = "普通";

    /// <summary>储值余额（元）。</summary>
    public decimal Balance { get; set; }

    /// <summary>积分。</summary>
    public int Points { get; set; }

    /// <summary>生日（可空，用来做生日提醒/优惠）。</summary>
    public DateTime? Birthday { get; set; }

    public string Remark { get; set; } = string.Empty;

    /// <summary>1=正常，99=已注销（注销后不再可用，但流水保留）。</summary>
    public int DataStatus { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>是否可用（未注销）。</summary>
    public bool IsActive => DataStatus == 1;

    /// <summary>
    /// 手机号脱敏（列表上默认显示脱敏号）。
    /// 收银台是开放环境，完整手机号挂在屏幕上会被旁边的人看走。
    /// </summary>
    public string MaskedPhone => Phone.Length == 11
        ? $"{Phone[..3]}****{Phone[7..]}"
        : Phone;
}

/// <summary>
/// 会员流水类型。
/// 编号固定，报表和流水列表按它分类，不要随意改。
/// </summary>
public enum MemberTransactionType
{
    /// <summary>充值：客人给钱，余额增加。</summary>
    Recharge = 1,

    /// <summary>消费：用余额付款，余额减少。</summary>
    Consume = 2,

    /// <summary>退款：把消费退回余额。</summary>
    Refund = 3,

    /// <summary>余额调整：商家手工改（必须有原因）。</summary>
    BalanceAdjust = 4,

    /// <summary>积分调整：商家手工改。</summary>
    PointAdjust = 5,

    /// <summary>注销退款：注销会员时把余额退给客人。</summary>
    CancelRefund = 6,
}

/// <summary>
/// 会员流水。
/// 每一笔都要留「变动前后」的余额和积分，
/// 否则事后对不上账时无法判断是哪一笔出的问题。
/// </summary>
public class MemberTransaction
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string CardNo { get; set; } = string.Empty;

    public string MemberName { get; set; } = string.Empty;

    public MemberTransactionType Type { get; set; }

    /// <summary>金额变动（正数增加、负数减少）。</summary>
    public decimal Amount { get; set; }

    /// <summary>积分变动（正负）。</summary>
    public int PointsChange { get; set; }

    /// <summary>变动后余额。</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>变动后积分。</summary>
    public int PointsAfter { get; set; }

    /// <summary>充值时的收款方式名（现金/扫码…）。</summary>
    public string PayTypeName { get; set; } = string.Empty;

    /// <summary>调整原因（余额调整/积分调整必填）。</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>关联的订单号（消费/退款时填）。</summary>
    public string OrderNo { get; set; } = string.Empty;

    public long OperatorId { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>类型中文名（界面显示）。</summary>
    public string TypeName => Type switch
    {
        MemberTransactionType.Recharge => "充值",
        MemberTransactionType.Consume => "消费",
        MemberTransactionType.Refund => "退款",
        MemberTransactionType.BalanceAdjust => "余额调整",
        MemberTransactionType.PointAdjust => "积分调整",
        MemberTransactionType.CancelRefund => "注销退款",
        _ => "其他",
    };
}