using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>
/// 订单仓储实现（T-003）。
///
/// 约定：
///   * 所有返回 Order 的查询都会填充 Items / Payments（聚合完整）；
///   * InsertAsync / UpdateAsync 中订单 + 明细 + 支付在同一事务内，失败全回滚；
///   * ClientOrderNo 为空时自动生成 UUID（N 格式）作为幂等键。
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly ISqlSugarClient _db;

    public OrderRepository(ISqlSugarClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Order?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var order = await _db.Queryable<Order>().Where(o => o.Id == id).FirstAsync();
        if (order == null)
            return null;
        await LoadAggregateAsync(order);
        return order;
    }

    public async Task<Order?> GetByClientOrderNoAsync(string clientOrderNo, CancellationToken ct = default)
    {
        var order = await _db.Queryable<Order>().Where(o => o.ClientOrderNo == clientOrderNo).FirstAsync();
        if (order == null)
            return null;
        await LoadAggregateAsync(order);
        return order;
    }

    public async Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = await _db.Queryable<Order>()
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
            .OrderBy(o => o.CreatedAt, OrderByType.Desc)
            .ToListAsync();
        foreach (var order in orders)
            await LoadAggregateAsync(order);
        return orders;
    }

    public async Task<List<Order>> GetPendingAsync(CancellationToken ct = default)
    {
        var orders = await _db.Queryable<Order>()
            .Where(o => o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt, OrderByType.Asc)
            .ToListAsync();
        foreach (var order in orders)
            await LoadAggregateAsync(order);
        return orders;
    }

    public async Task<List<Order>> GetByDeskIdAsync(long deskId, CancellationToken ct = default)
    {
        var orders = await _db.Queryable<Order>()
            .Where(o => o.DeskId == deskId)
            .OrderBy(o => o.CreatedAt, OrderByType.Desc)
            .ToListAsync();
        foreach (var order in orders)
            await LoadAggregateAsync(order);
        return orders;
    }

    public async Task<List<Order>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default)
    {
        var orders = await _db.Queryable<Order>()
            .Where(o => o.ShiftId == shiftId)
            .OrderBy(o => o.CreatedAt, OrderByType.Desc)
            .ToListAsync();
        foreach (var order in orders)
            await LoadAggregateAsync(order);
        return orders;
    }

    public async Task<List<Order>> GetUnsyncedAsync(CancellationToken ct = default)
    {
        var orders = await _db.Queryable<Order>()
            .Where(o => o.SyncStatus == SyncStatus.Pending)
            .OrderBy(o => o.CreatedAt, OrderByType.Asc)
            .ToListAsync();
        foreach (var order in orders)
            await LoadAggregateAsync(order);
        return orders;
    }

    /// <summary>
    /// 插入订单（含明细与支付），三者在同一事务内，任一步失败全部回滚。
    /// 返回新订单的自增 ID（同时回填到 <paramref name="order"/>.Id）。
    /// </summary>
    public async Task<long> InsertAsync(Order order, CancellationToken ct = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        if (string.IsNullOrWhiteSpace(order.ClientOrderNo))
            order.ClientOrderNo = Guid.NewGuid().ToString("N");
        if (order.CreatedAt == default)
            order.CreatedAt = EntityNormalizer.Now;
        EntityNormalizer.TruncateDates(order);

        long orderId = 0;
        await Tran.RunAsync(_db, async () =>
        {
            orderId = await _db.Insertable(order).ExecuteReturnIdentityAsync();

            var items = order.Items ?? new List<OrderItem>();
            foreach (var item in items)
            {
                item.OrderId = orderId;
                EntityNormalizer.TruncateDates(item);
            }
            if (items.Count > 0)
                await _db.Insertable(items).ExecuteCommandAsync();

            var payments = order.Payments ?? new List<Payment>();
            foreach (var payment in payments)
            {
                payment.OrderId = orderId;
                if (payment.PayTime == default)
                    payment.PayTime = EntityNormalizer.Now;
                EntityNormalizer.TruncateDates(payment);
            }
            if (payments.Count > 0)
                await _db.Insertable(payments).ExecuteCommandAsync();
        });

        order.Id = orderId;
        return orderId;
    }

    /// <summary>
    /// 更新订单：订单头 + 明细 + 支付整体替换（先删后插），同一事务内完成。
    /// </summary>
    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        EntityNormalizer.TruncateDates(order);

        await Tran.RunAsync(_db, async () =>
        {
            await _db.Updateable(order).ExecuteCommandAsync();
            await _db.Deleteable<OrderItem>().Where(i => i.OrderId == order.Id).ExecuteCommandAsync();
            await _db.Deleteable<Payment>().Where(p => p.OrderId == order.Id).ExecuteCommandAsync();

            var items = order.Items ?? new List<OrderItem>();
            foreach (var item in items)
            {
                item.OrderId = order.Id;
                EntityNormalizer.TruncateDates(item);
            }
            if (items.Count > 0)
                await _db.Insertable(items).ExecuteCommandAsync();

            var payments = order.Payments ?? new List<Payment>();
            foreach (var payment in payments)
            {
                payment.OrderId = order.Id;
                EntityNormalizer.TruncateDates(payment);
            }
            if (payments.Count > 0)
                await _db.Insertable(payments).ExecuteCommandAsync();
        });
    }

    /// <summary>
    /// 本地订单号生成（服务端不可用时的兜底）。
    /// 格式：L + yyyyMMdd + 4 位日期内序号，如 L20261012-0007。
    /// 序号基于 DictEntry 表按天递增，同一事务内读写，保证并发下不重号。
    /// </summary>
    public async Task<string> NextOrderNoAsync(CancellationToken ct = default)
    {
        var datePart = EntityNormalizer.Truncate(DateTime.Now).ToString("yyyyMMdd");
        var key = $"orderseq:{datePart}";
        var orderNo = string.Empty;

        await Tran.RunAsync(_db, async () =>
        {
            var entry = await _db.Queryable<DictEntry>().Where(d => d.Key == key).FirstAsync();
            long seq;
            if (entry == null)
            {
                seq = 1;
                await _db.Insertable(new DictEntry { Key = key, Value = "1", UpdatedAt = EntityNormalizer.Now })
                    .ExecuteCommandAsync();
            }
            else
            {
                seq = long.TryParse(entry.Value, out var v) ? v + 1 : 1;
                entry.Value = seq.ToString();
                entry.UpdatedAt = EntityNormalizer.Now;
                await _db.Updateable(entry).ExecuteCommandAsync();
            }
            orderNo = $"L{datePart}-{seq:D4}";
        });

        return orderNo;
    }

    private async Task LoadAggregateAsync(Order order)
    {
        order.Items = await _db.Queryable<OrderItem>()
            .Where(i => i.OrderId == order.Id)
            .OrderBy(i => i.Id, OrderByType.Asc)
            .ToListAsync();
        order.Payments = await _db.Queryable<Payment>()
            .Where(p => p.OrderId == order.Id)
            .OrderBy(p => p.Id, OrderByType.Asc)
            .ToListAsync();
    }
}
