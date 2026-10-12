namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 会员 DTO。
/// 注意：没有「有效期」字段（法规第十九条：记名卡不得设有效期）。
/// </summary>
public class MemberDto
{
    public long Id { get; set; }
    /// <summary>会员卡号（纯数字，不用手机号+生日）。</summary>
    public string CardNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>手机号：必填，唯一标识（实名）。</summary>
    public string Phone { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Level { get; set; } = "普通会员";
    public decimal TotalSpent { get; set; }
    public bool IsLost { get; set; }      // 挂失
    public bool IsDisabled { get; set; }  // 停用（≠删除）
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 余额流水 DTO。
/// </summary>
public class BalanceRecordDto
{
    public long Id { get; set; }
    public long MemberId { get; set; }
    public decimal Amount { get; set; }      // 正=增加，负=减少
    public decimal BalanceAfter { get; set; }
    public BalanceChangeReason Reason { get; set; }
    public string ReasonText { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 余额变动原因（选择列表，不让打字）。
/// </summary>
public enum BalanceChangeReason
{
    Recharge,     // 充值
    Consume,      // 消费
    Gift,         // 赠送
    Compensation, // 补偿（客人投诉）
    Correction,   // 纠错（点单点错）
    Refund,       // 退卡
    Other,        // 其他
}

/// <summary>
/// 优惠券 DTO（商家自发营销券，与 T-601 团购券分离）。
/// </summary>
public class CouponDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public CouponType Type { get; set; }
    /// <summary>满减门槛（满多少）。</summary>
    public decimal Threshold { get; set; }
    /// <summary>面值：满减=减多少，折扣=折扣率(0.9)，代金=金额，兑换=0。</summary>
    public decimal Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public bool IsUsed { get; set; }
    public long? BoundMemberId { get; set; }
    public DateTime? UsedAt { get; set; }
}

public enum CouponType
{
    FullReduction, // 满减
    Discount,      // 折扣
    Voucher,       // 代金
    Exchange,      // 兑换
}

/// <summary>
/// 会员仓储接口。
/// </summary>
public interface IMemberRepository
{
    Task<MemberDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<MemberDto?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task<List<MemberDto>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<MemberDto> CreateAsync(MemberDto member, CancellationToken ct = default);
    Task UpdateAsync(MemberDto member, CancellationToken ct = default);
    Task<List<BalanceRecordDto>> GetBalanceRecordsAsync(long memberId, CancellationToken ct = default);
    Task AddBalanceRecordAsync(BalanceRecordDto record, CancellationToken ct = default);
}

/// <summary>
/// 券仓储接口。
/// </summary>
public interface ICouponRepository
{
    Task<CouponDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<CouponDto>> GetByMemberAsync(long memberId, CancellationToken ct = default);
    Task CreateAsync(CouponDto coupon, CancellationToken ct = default);
    /// <summary>
    /// 核销（幂等）。返回 true=本次核销成功，false=已核销过。
    /// </summary>
    Task<bool> RedeemAsync(string code, long memberId, CancellationToken ct = default);
}
