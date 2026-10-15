using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>
/// 会员仓储。
///
/// 关键约束：**余额/积分的变动必须走 AddTransactionAsync**，
/// 它在同一个事务里「写流水 + 改余额」。
/// 分开写的话，中间任何一步失败都会让余额和流水对不上，
/// 而会员余额是客人的钱，对不上就是纠纷。
/// </summary>
public sealed class MemberRepository : IMemberRepository
{
    private readonly ISqlSugarClient _db;

    public MemberRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Member?> GetByCardNoAsync(string cardNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return null;
        var key = cardNo.Trim();
        return await _db.Queryable<Member>().Where(m => m.CardNo == key).FirstAsync();
    }

    public Task<List<Member>> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Task.FromResult(new List<Member>());

        var key = phone.Trim();
        return _db.Queryable<Member>()
            .Where(m => m.Phone == key)
            .OrderBy(m => m.Id, OrderByType.Desc)
            .ToListAsync();
    }

    public async Task<Member?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.Queryable<Member>().Where(m => m.Id == id).FirstAsync();

    public Task<List<Member>> SearchAsync(string keyword, int limit = 100, CancellationToken ct = default)
    {
        var query = _db.Queryable<Member>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(m =>
                m.CardNo.Contains(k) ||
                m.Phone.Contains(k) ||
                m.Name.Contains(k));
        }

        return query
            .OrderBy(m => m.DataStatus, OrderByType.Asc)   // 正常的排前面
            .OrderBy(m => m.Id, OrderByType.Desc)
            .Take(limit)
            .ToListAsync();
    }

    public Task<List<Member>> GetAllAsync(CancellationToken ct = default)
        => _db.Queryable<Member>()
            .OrderBy(m => m.DataStatus, OrderByType.Asc)
            .OrderBy(m => m.Id, OrderByType.Desc)
            .ToListAsync();

    public async Task<long> SaveAsync(Member member, CancellationToken ct = default)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));

        member.UpdatedAt = DateTime.Now;

        if (member.Id > 0)
        {
            await _db.Updateable(member).ExecuteCommandAsync();
            return member.Id;
        }

        member.CreatedAt = DateTime.Now;
        return await _db.Insertable(member).ExecuteReturnIdentityAsync();
    }

    public Task SetDataStatusAsync(long memberId, int dataStatus, CancellationToken ct = default)
        => _db.Updateable<Member>()
            .SetColumns(m => new Member
            {
                DataStatus = dataStatus,
                UpdatedAt = DateTime.Now,
            })
            .Where(m => m.Id == memberId)
            .ExecuteCommandAsync();

    /// <summary>
    /// 写流水 + 同步余额/积分，同一个事务。
    /// 流水里记的是「变动后」的值，所以以传入的 BalanceAfter/PointsAfter 为准回写会员，
    /// 不做增量累加 —— 增量累加在并发下会丢更新。
    /// </summary>
    public async Task<long> AddTransactionAsync(MemberTransaction tx, CancellationToken ct = default)
    {
        if (tx == null) throw new ArgumentNullException(nameof(tx));

        tx.CreatedAt = DateTime.Now;

        long id = 0;
        await _db.Ado.BeginTranAsync();
        try
        {
            id = await _db.Insertable(tx).ExecuteReturnIdentityAsync();

            if (tx.MemberId > 0)
            {
                await _db.Updateable<Member>()
                    .SetColumns(m => new Member
                    {
                        Balance = tx.BalanceAfter,
                        Points = tx.PointsAfter,
                        UpdatedAt = DateTime.Now,
                    })
                    .Where(m => m.Id == tx.MemberId)
                    .ExecuteCommandAsync();
            }

            await _db.Ado.CommitTranAsync();
        }
        catch
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }

        return id;
    }

    public Task<List<MemberTransaction>> GetTransactionsAsync(long memberId, int limit = 200, CancellationToken ct = default)
        => _db.Queryable<MemberTransaction>()
            .Where(t => t.MemberId == memberId)
            .OrderBy(t => t.Id, OrderByType.Desc)
            .Take(limit)
            .ToListAsync();

    public async Task<bool> CardNoExistsAsync(string cardNo, long excludeId = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return false;
        var key = cardNo.Trim();
        return await _db.Queryable<Member>()
            .Where(m => m.CardNo == key && m.Id != excludeId)
            .AnyAsync();
    }

    /// <summary>
    /// 生成新卡号：M + 6 位序号。
    /// 取现有最大数字后缀 +1，不复用已注销的号（注销的卡号要留档）。
    /// </summary>
    public async Task<string> NextCardNoAsync(CancellationToken ct = default)
    {
        var all = await _db.Queryable<Member>().Select(m => m.CardNo).ToListAsync();

        var max = 0;
        foreach (var card in all)
        {
            if (string.IsNullOrEmpty(card)) continue;
            var digits = new string(card.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, out var n) && n > max)
                max = n;
        }

        return $"M{max + 1:D6}";
    }
}