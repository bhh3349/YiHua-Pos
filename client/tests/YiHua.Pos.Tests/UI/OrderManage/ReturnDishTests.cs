using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.OrderManage;
using Xunit;

namespace YiHua.Pos.Tests.UI.OrderManage;

internal sealed class FakeOrderRepository : IOrderRepository
{
    public List<Order> Orders { get; } = new();
    public Order? LastInserted { get; private set; }

    public Task<Order?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
    public Task<Order?> GetByClientOrderNoAsync(string cno, CancellationToken ct = default) =>
        Task.FromResult<Order?>(null);
    public Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        Task.FromResult(Orders.Where(o => o.CreatedAt >= from && o.CreatedAt < to).ToList());
    public Task<List<Order>> GetPendingAsync(CancellationToken ct = default) =>
        Task.FromResult(Orders.Where(o => o.Status == OrderStatus.Pending).ToList());
    public Task<List<Order>> GetByDeskIdAsync(long deskId, CancellationToken ct = default) =>
        Task.FromResult(Orders.Where(o => o.DeskId == deskId).ToList());
    public Task<List<Order>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default) =>
        Task.FromResult(new List<Order>());
    public Task<List<Order>> GetUnsyncedAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<Order>());
    public Task<long> InsertAsync(Order order, CancellationToken ct = default)
    {
        LastInserted = order;
        Orders.Add(order);
        return Task.FromResult((long)Orders.Count);
    }
    public Task UpdateAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> NextOrderNoAsync(CancellationToken ct = default) => Task.FromResult("T001");
}

/// <summary>退菜测试：整道/部分/超量/已结账</summary>
public class ReturnDishTests
{
    private static (Order, OrderItem) MakeOrder(OrderStatus status = OrderStatus.Pending)
    {
        var item = new OrderItem { Id = 1, ProductName = "牛肉面", Price = 18m, Qty = 2, Amount = 36m };
        var order = new Order
        {
            Id = 1, OrderNo = "A20261012001", Status = status,
            Items = new List<OrderItem> { item },
            AmountTotal = 36m, AmountPay = 36m,
        };
        return (order, item);
    }

    [Fact]
    public async Task Refund_Pending_Modifies_Item_Directly()
    {
        var repo = new FakeOrderRepository();
        var (order, item) = MakeOrder(OrderStatus.Pending);
        var vm = new ReturnDishViewModel(repo, order, item) { RefundQty = 1, Reason = RefundReason.WrongDish };

        bool ok = await vm.ConfirmAsync();

        Assert.True(ok);
        Assert.Equal(1, item.Qty);          // 数量减少
        Assert.Equal(18m, item.Amount);     // 重算小计
        Assert.Equal(18m, order.AmountPay); // 重算应收
    }

    [Fact]
    public async Task Refund_Settled_Does_Not_Modify_Original()
    {
        var repo = new FakeOrderRepository();
        var (order, item) = MakeOrder(OrderStatus.Settled);
        var vm = new ReturnDishViewModel(repo, order, item) { RefundQty = 2, Reason = RefundReason.QualityIssue };

        bool ok = await vm.ConfirmAsync();

        Assert.True(ok);
        Assert.Equal(2, item.Qty);              // 原单明细不动
        Assert.True(item.IsRefunded);           // 标记已退
        Assert.Equal(36m, order.AmountRefund);  // 退菜金额累计
    }

    [Fact]
    public async Task Refund_OverQty_Rejected()
    {
        var repo = new FakeOrderRepository();
        var (order, item) = MakeOrder();
        var vm = new ReturnDishViewModel(repo, order, item) { RefundQty = 3 };

        bool ok = await vm.ConfirmAsync();

        Assert.False(ok);
        Assert.Contains("超过", vm.ErrorMessage);
    }
}
