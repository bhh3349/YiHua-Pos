using Xunit;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Tests.Data;

public class OrderRepositoryTests : TempDbFixture
{
    private readonly OrderRepository _repo;

    public OrderRepositoryTests()
    {
        _repo = new OrderRepository(Ctx.Db);
    }

    private static Order NewOrder(string? clientOrderNo = null) => new()
    {
        ClientOrderNo = clientOrderNo ?? Guid.NewGuid().ToString("N"),
        DeskId = 1,
        ShiftId = 1,
        OperatorId = 100,
        AmountTotal = 59.98m,
        AmountPay = 59.98m,
        AmountReceived = 60m,
        AmountChange = 0.02m,
        CreatedAt = DateTime.Now,
        Items =
        {
            new OrderItem { ProductId = 11, ProductName = "宫保鸡丁", Price = 29.99m, Qty = 2, Amount = 59.98m },
        },
        Payments =
        {
            new Payment { PayType = PayType.Cash, Amount = 60m, OperatorId = 100 },
        },
    };

    [Fact]
    public async Task Insert_LoadsFullAggregate()
    {
        var order = NewOrder();

        var id = await _repo.InsertAsync(order);

        Assert.True(id > 0);
        Assert.Equal(id, order.Id);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
        Assert.Single(loaded.Payments);
        Assert.Equal("宫保鸡丁", loaded.Items[0].ProductName);
        Assert.Equal(PayType.Cash, loaded.Payments[0].PayType);
    }

    [Fact]
    public async Task Insert_PreservesDecimalPrecision()
    {
        // 金额必须精确往返（19.99 这类值最容易暴露浮点存储问题）
        var order = NewOrder();
        var id = await _repo.InsertAsync(order);

        var loaded = await _repo.GetByIdAsync(id);
        Assert.Equal(59.98m, loaded!.AmountTotal);
        Assert.Equal(0.02m, loaded.AmountChange);
        Assert.Equal(29.99m, loaded.Items[0].Price);
    }

    [Fact]
    public async Task Insert_DuplicateClientOrderNo_ThrowsAndLeavesDbUnchanged()
    {
        var dupNo = Guid.NewGuid().ToString("N");
        await _repo.InsertAsync(NewOrder(dupNo));

        // 重复幂等键必须抛异常（唯一索引生效）
        await Assert.ThrowsAnyAsync<Exception>(() => _repo.InsertAsync(NewOrder(dupNo)));

        // 且没有产生脏数据：订单仍只有 1 条，明细/支付未残留
        var count = await Ctx.Db.Queryable<Order>().CountAsync();
        Assert.Equal(1, count);
        var itemCount = await Ctx.Db.Queryable<OrderItem>().CountAsync();
        var payCount = await Ctx.Db.Queryable<Payment>().CountAsync();
        Assert.Equal(1, itemCount);
        Assert.Equal(1, payCount);
    }

    [Fact]
    public async Task Update_ReplacesItemsAndPaymentsAtomically()
    {
        var id = await _repo.InsertAsync(NewOrder());

        var loaded = (await _repo.GetByIdAsync(id))!;
        loaded.Remark = "加菜";
        loaded.Items.Clear();
        loaded.Items.Add(new OrderItem { ProductId = 22, ProductName = "米饭", Price = 2m, Qty = 1, Amount = 2m });
        loaded.Payments.Clear(); // 本次未支付

        await _repo.UpdateAsync(loaded);

        var reloaded = await _repo.GetByIdAsync(id);
        Assert.Equal("加菜", reloaded!.Remark);
        Assert.Single(reloaded.Items);
        Assert.Equal("米饭", reloaded.Items[0].ProductName);
        Assert.Empty(reloaded.Payments);
    }

    [Fact]
    public async Task GetByClientOrderNo_Works()
    {
        var no = Guid.NewGuid().ToString("N");
        await _repo.InsertAsync(NewOrder(no));

        var found = await _repo.GetByClientOrderNoAsync(no);
        Assert.NotNull(found);
        Assert.Single(found!.Items);

        var missing = await _repo.GetByClientOrderNoAsync("not-exists");
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetByDateRange_FiltersCorrectly()
    {
        var baseTime = new DateTime(2026, 10, 12, 10, 0, 0);
        var a = NewOrder(); a.CreatedAt = baseTime;
        var b = NewOrder(); b.CreatedAt = baseTime.AddHours(2);
        var c = NewOrder(); c.CreatedAt = baseTime.AddDays(1);
        await _repo.InsertAsync(a);
        await _repo.InsertAsync(b);
        await _repo.InsertAsync(c);

        var result = await _repo.GetByDateRangeAsync(baseTime, baseTime.AddHours(3));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetPending_And_GetUnsynced_FilterCorrectly()
    {
        var o1 = NewOrder(); // Pending + 未同步
        await _repo.InsertAsync(o1);

        var o2 = NewOrder();
        o2.Status = OrderStatus.Settled;
        o2.SettleTime = DateTime.Now;
        await _repo.InsertAsync(o2);

        var pending = await _repo.GetPendingAsync();
        Assert.Single(pending);

        var unsynced = await _repo.GetUnsyncedAsync();
        Assert.Equal(2, unsynced.Count);

        // 标记一条已同步后，未同步只剩 1 条
        o1.SyncStatus = SyncStatus.Succeeded;
        await _repo.UpdateAsync(o1);
        Assert.Single(await _repo.GetUnsyncedAsync());
    }

    [Fact]
    public async Task NextOrderNo_IncrementsDaily()
    {
        var n1 = await _repo.NextOrderNoAsync();
        var n2 = await _repo.NextOrderNoAsync();

        Assert.NotEqual(n1, n2);
        var today = DateTime.Now.ToString("yyyyMMdd");
        Assert.StartsWith($"L{today}-", n1);
        Assert.StartsWith($"L{today}-", n2);

        // 序号递增
        var s1 = int.Parse(n1.Split('-')[1]);
        var s2 = int.Parse(n2.Split('-')[1]);
        Assert.Equal(s1 + 1, s2);
    }

    [Fact]
    public async Task CreatedAt_StoredInRequiredStringFormat()
    {
        var id = await _repo.InsertAsync(NewOrder());

        // 直接读原始存储值，验收 yyyy-MM-dd HH:mm:ss 字符串格式
        var raw = Ctx.Db.Ado.GetString($"SELECT CreatedAt FROM \"Order\" WHERE Id={id}");
        Assert.Equal(19, raw.Length); // yyyy-MM-dd HH:mm:ss 恰好 19 字符
        Assert.True(DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _));
    }
}
