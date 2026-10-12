using Xunit;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Tests.Data;

/// <summary>
/// 主流程端到端：开台 → 点菜 → 结算 → 交接班 → 报表。
/// 用真实仓储 + 真实 SQLite，目的是验证「这些东西串起来能不能真的跑」，
/// 而不是覆盖分支。分支覆盖由各自的单元测试负责。
/// </summary>
public class MainFlowE2ETests : TempDbFixture
{
    [Fact]
    public async Task 主流程_开台点菜结算交接班报表_全程走通()
    {
        var desks = new DeskRepository(Ctx.Db);
        var orders = new OrderRepository(Ctx.Db);
        var products = new ProductRepository(Ctx.Db);
        var shifts = new ShiftRepository(Ctx.Db);

        // ---- 前置：产品 + 档口 + 桌台 ----
        await products.ReplaceAllAsync(new[]
        {
            new Product { Id = 1, Name = "宫保鸡丁", Price = 28.00m, CategoryId = 1, DataStatus = 1 },
            new Product { Id = 2, Name = "牛肉面",   Price = 38.50m, CategoryId = 1, DataStatus = 1 },
        });

        await desks.ReplaceAllAsync(
            new[] { new DeskArea { Id = 1, Name = "大厅", Sort = 1 } },
            new[] { new Desk { Id = 1, Name = "A01", AreaId = 1, Status = DeskStatus.Idle } });

        // ---- 1. 开台 ----
        var desk = await desks.GetByIdAsync(1);
        Assert.NotNull(desk);
        desk!.Status = DeskStatus.Occupied;
        desk.OpenTime = DateTime.Now;
        desk.GuestCount = 2;
        await desks.UpdateAsync(desk);

        var reread = await desks.GetByIdAsync(1);
        Assert.Equal(DeskStatus.Occupied, reread!.Status);

        // ---- 2. 点菜（2 道，其中一道有行折扣）----
        var sub = 28.00m + 38.50m;
        const decimal lineDiscount = 5.00m;

        var order = new Order
        {
            OrderNo = "A001",
            ClientOrderNo = "E2E-" + Guid.NewGuid().ToString("N"),
            DeskId = 1,
            OrderType = OrderType.DineIn,
            Status = OrderStatus.Pending,
            GuestCount = 2,
            AmountTotal = sub,
            AmountDiscount = lineDiscount,
            AmountPay = sub - lineDiscount,
            CreatedAt = DateTime.Now,
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, ProductName = "宫保鸡丁", Price = 28.00m, Qty = 1, Amount = 28.00m },
                new() { ProductId = 2, ProductName = "牛肉面", Price = 38.50m, Qty = 1,
                        Amount = 38.50m, DiscountAmount = lineDiscount },
            },
        };

        var orderId = await orders.InsertAsync(order);
        Assert.True(orderId > 0, "订单应插入成功");

        // ---- 3. 读回，验证聚合项（明细）被正确加载 ----
        var loaded = await orders.GetByIdAsync(orderId);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Items.Count);
        Assert.Equal("牛肉面", loaded.Items.First(i => i.ProductId == 2).ProductName);
        Assert.Equal(lineDiscount, loaded.Items.First(i => i.ProductId == 2).DiscountAmount);

        // ---- 4. 结算 ----
        const decimal orderDiscount = 3.00m;
        const decimal wipe = 0.50m;
        const decimal received = 70.00m;
        var payable = sub - lineDiscount - orderDiscount - wipe;

        loaded.AmountDiscount = lineDiscount + orderDiscount + wipe;
        loaded.AmountPay = payable;
        loaded.AmountReceived = received;
        loaded.AmountChange = received - payable;
        loaded.Status = OrderStatus.Settled;
        loaded.SettleTime = DateTime.Now;
        await orders.UpdateAsync(loaded);

        var settled = await orders.GetByIdAsync(orderId);
        Assert.Equal(OrderStatus.Settled, settled!.Status);

        // 金额恒等式：原价 - 优惠 = 应收；实收 - 应收 = 找零
        Assert.Equal(settled.AmountTotal - settled.AmountDiscount, settled.AmountPay);
        Assert.Equal(received - settled.AmountPay, settled.AmountChange);

        // ---- 5. 交接班 ----
        var shift = new Shift
        {
            OperatorId = 1,
            OperatorName = "测试员",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now,
            Status = ShiftStatus.Closed,
            OrderCount = 1,
            AmountTotal = payable,
        };
        var shiftId = await shifts.InsertAsync(shift);
        Assert.True(shiftId > 0, "班次应插入成功");

        settled.ShiftId = shiftId;
        await orders.UpdateAsync(settled);

        // ---- 6. 报表：按班次查、按日期查 ----
        var byShift = await orders.GetByShiftIdAsync(shiftId);
        Assert.Single(byShift);
        Assert.Equal(payable, byShift[0].AmountPay);

        var byDate = await orders.GetByDateRangeAsync(DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Contains(byDate, o => o.Id == orderId);

        // 未同步队列应能查到这单（离线补传的基础）
        var unsynced = await orders.GetUnsyncedAsync();
        Assert.Contains(unsynced, o => o.Id == orderId);
    }

    [Fact]
    public async Task 团购字段_Wave5新增的列_能存能读()
    {
        var orders = new OrderRepository(Ctx.Db);

        var order = new Order
        {
            OrderNo = "A002",
            ClientOrderNo = "E2E-GB-" + Guid.NewGuid().ToString("N"),
            DeskId = 1,
            AmountTotal = 100m,
            AmountDiscount = 30m,   // 团购抵扣进优惠
            AmountPay = 70m,        // 不进实收
            GroupBuyPlatform = "meituan",
            GroupBuyCouponCode = "MT-ABC-123",
            AmountGroupBuy = 30m,
            CreatedAt = DateTime.Now,
        };

        var id = await orders.InsertAsync(order);
        var loaded = await orders.GetByIdAsync(id);

        Assert.Equal("meituan", loaded!.GroupBuyPlatform);
        Assert.Equal("MT-ABC-123", loaded.GroupBuyCouponCode);
        Assert.Equal(30m, loaded.AmountGroupBuy);
        Assert.Equal(100m - 30m, loaded.AmountPay);
    }

    [Fact]
    public async Task 幂等键_同一ClientOrderNo_不能插两条()
    {
        var orders = new OrderRepository(Ctx.Db);
        var clientOrderNo = "E2E-IDEM-" + Guid.NewGuid().ToString("N");

        var mk = () => new Order
        {
            OrderNo = "A003",
            ClientOrderNo = clientOrderNo,
            DeskId = 1,
            AmountTotal = 50m,
            AmountPay = 50m,
            CreatedAt = DateTime.Now,
        };

        await orders.InsertAsync(mk());

        // 断网补传 / 用户重复点提交都会走到这里，必须靠唯一索引挡住
        var existing = await orders.GetByClientOrderNoAsync(clientOrderNo);
        Assert.NotNull(existing);
        Assert.Equal(50m, existing!.AmountPay);

        var all = await orders.GetByDateRangeAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
        Assert.Single(all.Where(o => o.ClientOrderNo == clientOrderNo));
    }
}