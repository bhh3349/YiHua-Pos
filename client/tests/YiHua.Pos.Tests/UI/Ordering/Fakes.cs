using Xunit;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Ordering;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.Tests.UI.Ordering;

// 别名放在 namespace 内部：文件层级的 using 属于全局作用域，而
// YiHua.Pos.UI.ViewModels 下有 Desk / Product 两个子命名空间参与名字解析，
// 更近的作用域优先，全局别名会被跳过，导致 CS0118。
using Desk = YiHua.Pos.Domain.Entities.Desk;
using Product = YiHua.Pos.Domain.Entities.Product;

/// <summary>测试用假仓储</summary>
public sealed class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = new();

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(Products.ToList());

    public Task<List<Product>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
        => Task.FromResult(Products.Where(p => p.CategoryId == categoryId).ToList());

    public Task<Product?> GetByIdAsync(long id, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p => p.Barcode == barcode));

    public Task<List<Product>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default)
        => Task.FromResult(Products.Where(p => p.Name.Contains(keyword)).Take(limit).ToList());

    public Task<long> SaveAsync(Product product, CancellationToken ct = default)
    {
        if (product.Id <= 0)
        {
            product.Id = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
            Products.Add(product);
        }
        else
        {
            var idx = Products.FindIndex(p => p.Id == product.Id);
            if (idx >= 0) Products[idx] = product; else Products.Add(product);
        }
        return Task.FromResult(product.Id);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default)
    {
        Products.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }

    public Task SetSoldOutAsync(long id, bool soldOut, DateTime? at = null, CancellationToken ct = default)
    {
        var p = Products.FirstOrDefault(x => x.Id == id);
        if (p != null) { p.IsSoldOut = soldOut; p.SoldOutAt = soldOut ? (at ?? DateTime.Now) : null; }
        return Task.CompletedTask;
    }

    public Task<int> BatchUpdatePriceAsync(IEnumerable<long> ids, decimal? newPrice, decimal? rate, CancellationToken ct = default)
    {
        var affected = 0;
        foreach (var id in ids)
        {
            var p = Products.FirstOrDefault(x => x.Id == id);
            if (p == null) continue;
            p.Price = newPrice ?? Math.Round(p.Price * rate!.Value, 2, MidpointRounding.AwayFromZero);
            affected++;
        }
        return Task.FromResult(affected);
    }

    public Task<int> BatchUpdateStatusAsync(IEnumerable<long> ids, int dataStatus, CancellationToken ct = default)
    {
        var affected = 0;
        foreach (var id in ids)
        {
            var p = Products.FirstOrDefault(x => x.Id == id);
            if (p == null) continue;
            p.DataStatus = dataStatus;
            affected++;
        }
        return Task.FromResult(affected);
    }

    public Task ReplaceAllAsync(IEnumerable<Product> products, CancellationToken ct = default)
    {
        Products.Clear();
        Products.AddRange(products);
        return Task.CompletedTask;
    }
}

public sealed class FakeCategoryRepository : ICategoryRepository
{
    public List<ProductCategory> Categories { get; } = new();

    public Task<List<ProductCategory>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(Categories.ToList());

    public Task<long> SaveAsync(ProductCategory category, CancellationToken ct = default)
    {
        if (category.Id <= 0)
        {
            category.Id = Categories.Count == 0 ? 1 : Categories.Max(c => c.Id) + 1;
            Categories.Add(category);
        }
        else
        {
            var idx = Categories.FindIndex(c => c.Id == category.Id);
            if (idx >= 0) Categories[idx] = category; else Categories.Add(category);
        }
        return Task.FromResult(category.Id);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default)
    {
        Categories.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReplaceAllAsync(IEnumerable<ProductCategory> categories, CancellationToken ct = default)
    {
        Categories.Clear();
        Categories.AddRange(categories);
        return Task.CompletedTask;
    }
}

public sealed class FakeOrderRepository : IOrderRepository
{
    private long _nextId = 1;
    public List<Order> Orders { get; } = new();
    public int InsertCalls { get; private set; }

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
        InsertCalls++;
        order.Id = _nextId++;
        Orders.Add(order);
        return Task.FromResult(order.Id);
    }

    public Task UpdateAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> NextOrderNoAsync(CancellationToken ct = default)
        => Task.FromResult($"T{DateTime.Now:yyyyMMdd}{_nextId:D4}");
}

public sealed class FakeOutboxRepository : IOutboxRepository
{
    public List<OutboxItem> Items { get; } = new();

    public Task<List<OutboxItem>> GetPendingAsync(int limit = 100, CancellationToken ct = default)
        => Task.FromResult(Items.Take(limit).ToList());

    public Task<long> EnqueueAsync(OutboxItem item, CancellationToken ct = default)
    {
        Items.Add(item);
        return Task.FromResult((long)Items.Count);
    }

    public Task MarkSucceededAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
    public Task MarkFailedAsync(long id, string error, CancellationToken ct = default) => Task.CompletedTask;
    public Task<int> CountPendingAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);
}

public static class OrderingTestFactory
{
    public static Product Noodle() => new()
    {
        Id = 1, Name = "牛肉面", Price = 18m, UnitName = "碗",
        CategoryId = 10, ProductType = 1, DataStatus = 1,
    };

    public static Product KungPao() => new()
    {
        Id = 2, Name = "宫保鸡丁", Price = 38m, UnitName = "份",
        CategoryId = 10, ProductType = 1, DataStatus = 1,
        SpecGroupName = "辣度",
    };

    public static Product Seafood() => new()
    {
        Id = 3, Name = "海鲜拼盘", Price = 68m, UnitName = "kg",
        CategoryId = 10, ProductType = 3, WeightWay = 1, DataStatus = 1,
    };

    /// <summary>沽清（今天卖完了）。注意：沽清 ≠ 下架，用 IsSoldOut 表达。</summary>
    public static Product SoldOut() => new()
    {
        Id = 4, Name = "沽清空盘", Price = 99m, UnitName = "份",
        CategoryId = 10, ProductType = 1, DataStatus = 1, IsSoldOut = true,
    };

    /// <summary>下架（老板主动停售）。</summary>
    public static Product OffShelf() => new()
    {
        Id = 5, Name = "已下架菜", Price = 20m, UnitName = "份",
        CategoryId = 10, ProductType = 1, DataStatus = 99,
    };

    public static OrderEntryViewModel CreateVm(
        OperatorRole role = OperatorRole.Cashier,
        Desk? desk = null,
        FakeOrderRepository? orderRepo = null)
    {
        var products = new FakeProductRepository();
        products.Products.AddRange(new[] { Noodle(), KungPao(), Seafood(), SoldOut(), OffShelf() });
        var cats = new FakeCategoryRepository();
        cats.Categories.Add(new ProductCategory { Id = 10, Name = "热销" });
        return new OrderEntryViewModel(
            products, cats,
            orderRepo ?? new FakeOrderRepository(),
            new FakeOutboxRepository(),
            new HangOrderStore(),
            role,
            desk ?? new Desk { Id = 8, Name = "A08", GuestCount = 4, OpenTime = DateTime.Now.AddMinutes(-25) });
    }
}
