using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Tests.UI.HangOrder;

// ViewModels.Desk 命名空间已存在，用别名避免 CS0118
using Desk = YiHua.Pos.Domain.Entities.Desk;
using DeskArea = YiHua.Pos.Domain.Entities.DeskArea;

public sealed class FakeDeskRepository : IDeskRepository
{
    public List<Desk> Desks { get; } = new();
    public List<DeskArea> Areas { get; } = new();

    public Task<List<DeskArea>> GetAreasAsync(CancellationToken ct = default)
        => Task.FromResult(Areas.ToList());

    public Task<List<Desk>> GetDesksAsync(long? areaId = null, CancellationToken ct = default)
        => Task.FromResult(areaId == null ? Desks.ToList() : Desks.Where(d => d.AreaId == areaId).ToList());

    public Task<Desk?> GetByIdAsync(long id, CancellationToken ct = default)
        => Task.FromResult(Desks.FirstOrDefault(d => d.Id == id));

    public Task UpdateAsync(Desk desk, CancellationToken ct = default)
    {
        var existing = Desks.FirstOrDefault(d => d.Id == desk.Id);
        if (existing != null)
        {
            existing.Status = desk.Status;
            existing.CurrentOrderId = desk.CurrentOrderId;
        }
        return Task.CompletedTask;
    }

    public Task ReplaceAllAsync(IEnumerable<DeskArea> areas, IEnumerable<Desk> desks, CancellationToken ct = default)
    {
        Areas.Clear(); Areas.AddRange(areas);
        Desks.Clear(); Desks.AddRange(desks);
        return Task.CompletedTask;
    }
}

public sealed class FakeOrderRepository2 : IOrderRepository
{
    public List<Order> Orders { get; } = new();
    private long _nextId = 100;

    public Task<Order?> GetByIdAsync(long id, CancellationToken ct = default)
        => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));

    public Task<Order?> GetByClientOrderNoAsync(string clientOrderNo, CancellationToken ct = default)
        => Task.FromResult(Orders.FirstOrDefault(o => o.ClientOrderNo == clientOrderNo));

    public Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult(Orders.Where(o => o.CreatedAt >= from && o.CreatedAt <= to).ToList());

    public Task<List<Order>> GetPendingAsync(CancellationToken ct = default)
        => Task.FromResult(Orders.Where(o => o.Status == OrderStatus.Pending).ToList());

    public Task<List<Order>> GetByDeskIdAsync(long deskId, CancellationToken ct = default)
        => Task.FromResult(Orders.Where(o => o.DeskId == deskId).ToList());

    public Task<List<Order>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default)
        => Task.FromResult(Orders.Where(o => o.ShiftId == shiftId).ToList());

    public Task<List<Order>> GetUnsyncedAsync(CancellationToken ct = default)
        => Task.FromResult(Orders.Where(o => o.SyncStatus == SyncStatus.Pending).ToList());

    public Task<long> InsertAsync(Order order, CancellationToken ct = default)
    {
        order.Id = _nextId++;
        Orders.Add(order);
        return Task.FromResult(order.Id);
    }

    public Task UpdateAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> NextOrderNoAsync(CancellationToken ct = default)
        => Task.FromResult($"T{_nextId:D4}");
}

public static class HangOrderTestFactory
{
    public static Desk DeskA08() => new()
    {
        Id = 8, Name = "A08", AreaId = 1,
        Status = DeskStatus.Occupied, CurrentOrderId = 1,
    };

    public static Desk DeskA09() => new()
    {
        Id = 9, Name = "A09", AreaId = 1,
        Status = DeskStatus.Idle, CurrentOrderId = 0,
    };

    public static Order SampleOrder(long deskId = 8)
    {
        var order = new Order
        {
            Id = 1, DeskId = deskId, Status = OrderStatus.Pending,
            GuestCount = 4, AmountPay = 74m, CreatedAt = DateTime.Now,
        };
        order.Items.Add(new OrderItem { Id = 101, ProductName = "牛肉面", Price = 18m, Qty = 2, Amount = 36m });
        order.Items.Add(new OrderItem { Id = 102, ProductName = "宫保鸡丁", Price = 38m, Qty = 1, Amount = 38m });
        return order;
    }
}
