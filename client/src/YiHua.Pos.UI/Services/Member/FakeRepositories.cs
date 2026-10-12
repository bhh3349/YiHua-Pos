namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 假会员仓储（测试/开发用）。
/// </summary>
public class FakeMemberRepository : IMemberRepository
{
    private readonly List<MemberDto> _members = new();
    private readonly List<BalanceRecordDto> _records = new();
    private long _nextId = 1;

    public Task<MemberDto?> GetByIdAsync(long id, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.Id == id));

    public Task<MemberDto?> GetByPhoneAsync(string phone, CancellationToken ct = default)
        => Task.FromResult(_members.FirstOrDefault(m => m.Phone == phone?.Trim()));

    public Task<List<MemberDto>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var kw = keyword?.Trim() ?? "";
        var result = _members.Where(m =>
            m.Name.Contains(kw) || m.Phone.Contains(kw) || m.CardNo.Contains(kw)).ToList();
        return Task.FromResult(result);
    }

    public Task<MemberDto> CreateAsync(MemberDto member, CancellationToken ct = default)
    {
        // 手机号唯一
        if (_members.Any(m => m.Phone == member.Phone))
            throw new InvalidOperationException($"手机号 {member.Phone} 已开过卡，不能重复开卡");
        member.Id = _nextId++;
        member.CardNo = $"88{member.Id:D8}"; // 纯数字卡号
        _members.Add(member);
        return Task.FromResult(member);
    }

    public Task UpdateAsync(MemberDto member, CancellationToken ct = default)
    {
        var existing = _members.FirstOrDefault(m => m.Id == member.Id);
        if (existing != null)
        {
            existing.Name = member.Name;
            existing.Balance = member.Balance;
            existing.Level = member.Level;
            existing.IsLost = member.IsLost;
            existing.IsDisabled = member.IsDisabled;
            existing.TotalSpent = member.TotalSpent;
        }
        return Task.CompletedTask;
    }

    public Task<List<BalanceRecordDto>> GetBalanceRecordsAsync(long memberId, CancellationToken ct = default)
        => Task.FromResult(_records.Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.CreatedAt).ToList());

    public Task AddBalanceRecordAsync(BalanceRecordDto record, CancellationToken ct = default)
    {
        record.Id = _records.Count + 1;
        _records.Add(record);
        return Task.CompletedTask;
    }

    /// <summary>测试用：预置会员。</summary>
    public void Seed(MemberDto member)
    {
        member.Id = _nextId++;
        if (string.IsNullOrEmpty(member.CardNo))
            member.CardNo = $"88{member.Id:D8}";
        _members.Add(member);
    }
}

/// <summary>
/// 假券仓储（测试/开发用，幂等核销）。
/// </summary>
public class FakeCouponRepository : ICouponRepository
{
    private readonly List<CouponDto> _coupons = new();
    private long _nextId = 1;
    private readonly object _lock = new();

    public Task<CouponDto?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_coupons.FirstOrDefault(c => c.Code == code));

    public Task<List<CouponDto>> GetByMemberAsync(long memberId, CancellationToken ct = default)
        => Task.FromResult(_coupons.Where(c => c.BoundMemberId == memberId).ToList());

    public Task CreateAsync(CouponDto coupon, CancellationToken ct = default)
    {
        coupon.Id = _nextId++;
        _coupons.Add(coupon);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 幂等核销：加锁，同一张券只成功一次。
    /// </summary>
    public Task<bool> RedeemAsync(string code, long memberId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var coupon = _coupons.FirstOrDefault(c => c.Code == code);
            if (coupon == null || coupon.IsUsed)
                return Task.FromResult(false); // 已用过或不存在，返回 false（幂等）
            if (coupon.BoundMemberId != memberId)
                return Task.FromResult(false);
            coupon.IsUsed = true;
            coupon.UsedAt = DateTime.Now;
            return Task.FromResult(true);
        }
    }

    /// <summary>测试用：预置券。</summary>
    public void Seed(CouponDto coupon)
    {
        coupon.Id = _nextId++;
        _coupons.Add(coupon);
    }
}
