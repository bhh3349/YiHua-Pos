using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>桌台仓储实现（T-003）。</summary>
public sealed class DeskRepository : IDeskRepository
{
    private readonly ISqlSugarClient _db;

    public DeskRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<DeskArea>> GetAreasAsync(CancellationToken ct = default)
        => _db.Queryable<DeskArea>()
            .OrderBy(a => a.Sort, OrderByType.Asc)
            .OrderBy(a => a.Id, OrderByType.Asc)
            .ToListAsync();

    public Task<List<Desk>> GetDesksAsync(long? areaId = null, CancellationToken ct = default)
    {
        var q = _db.Queryable<Desk>();
        if (areaId.HasValue)
            q = q.Where(d => d.AreaId == areaId.Value);
        return q.OrderBy(d => d.Sort, OrderByType.Asc)
                .OrderBy(d => d.Id, OrderByType.Asc)
                .ToListAsync();
    }

    public async Task<Desk?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.Queryable<Desk>().Where(d => d.Id == id).FirstAsync();

    public Task UpdateAsync(Desk desk, CancellationToken ct = default)
    {
        if (desk == null)
            throw new ArgumentNullException(nameof(desk));
        EntityNormalizer.TruncateDates(desk);
        return _db.Updateable(desk).ExecuteCommandAsync();
    }

    /// <summary>区域 + 桌台整体替换（服务端同步用），同一事务内完成。</summary>
    public async Task ReplaceAllAsync(IEnumerable<DeskArea> areas, IEnumerable<Desk> desks, CancellationToken ct = default)
    {
        var areaList = areas?.ToList() ?? new List<DeskArea>();
        var deskList = desks?.ToList() ?? new List<Desk>();
        foreach (var d in deskList) EntityNormalizer.TruncateDates(d);
        await Tran.RunAsync(_db, async () =>
        {
            await _db.Deleteable<Desk>().ExecuteCommandAsync();
            await _db.Deleteable<DeskArea>().ExecuteCommandAsync();
            if (areaList.Count > 0)
                await _db.Insertable(areaList).ExecuteCommandAsync();
            if (deskList.Count > 0)
                await _db.Insertable(deskList).ExecuteCommandAsync();
        });
    }
}

/// <summary>交班仓储实现（T-003）。</summary>
public sealed class ShiftRepository : IShiftRepository
{
    private readonly ISqlSugarClient _db;

    public ShiftRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>当前进行中的班次（Status=Open，取最近开班的一条）。</summary>
    public async Task<Shift?> GetCurrentAsync(CancellationToken ct = default)
        => await _db.Queryable<Shift>()
            .Where(s => s.Status == Domain.Enums.ShiftStatus.Open)
            .OrderBy(s => s.StartAt, OrderByType.Desc)
            .FirstAsync();

    public async Task<Shift?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.Queryable<Shift>().Where(s => s.Id == id).FirstAsync();

    public Task<List<Shift>> GetHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => _db.Queryable<Shift>()
            .Where(s => s.StartAt >= from && s.StartAt <= to)
            .OrderBy(s => s.StartAt, OrderByType.Desc)
            .ToListAsync();

    public async Task<long> InsertAsync(Shift shift, CancellationToken ct = default)
    {
        if (shift == null)
            throw new ArgumentNullException(nameof(shift));
        if (shift.StartAt == default)
            shift.StartAt = EntityNormalizer.Now;
        EntityNormalizer.TruncateDates(shift);
        var id = await _db.Insertable(shift).ExecuteReturnIdentityAsync();
        shift.Id = id;
        return id;
    }

    public Task UpdateAsync(Shift shift, CancellationToken ct = default)
    {
        if (shift == null)
            throw new ArgumentNullException(nameof(shift));
        EntityNormalizer.TruncateDates(shift);
        return _db.Updateable(shift).ExecuteCommandAsync();
    }
}
