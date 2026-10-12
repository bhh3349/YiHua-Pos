using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.Domain.Interfaces;

// ============================================================================
//  仓储接口 —— 【冻结文件】由 Lead 维护，实现者不得修改
//
//  T-003 实现者：在 src/YiHua.Pos.Data/Repositories/ 下实现这些接口
//  其他模块：只依赖这些接口，不依赖具体实现
// ============================================================================

/// <summary>订单仓储</summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Order?> GetByClientOrderNoAsync(string clientOrderNo, CancellationToken ct = default);
    Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<Order>> GetPendingAsync(CancellationToken ct = default);
    Task<List<Order>> GetByDeskIdAsync(long deskId, CancellationToken ct = default);
    Task<List<Order>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default);
    Task<List<Order>> GetUnsyncedAsync(CancellationToken ct = default);

    /// <summary>插入订单（含明细与支付），返回订单 ID</summary>
    Task<long> InsertAsync(Order order, CancellationToken ct = default);

    Task UpdateAsync(Order order, CancellationToken ct = default);

    /// <summary>本地订单号生成（服务端不可用时用）</summary>
    Task<string> NextOrderNoAsync(CancellationToken ct = default);
}

/// <summary>商品仓储（读多写少）</summary>
public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(CancellationToken ct = default);
    Task<List<Product>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);

    /// <summary>按名称 / 首字母 / 编码模糊搜索</summary>
    Task<List<Product>> SearchAsync(string keyword, int limit = 50, CancellationToken ct = default);

    /// <summary>批量替换（同步用）</summary>
    Task ReplaceAllAsync(IEnumerable<Product> products, CancellationToken ct = default);
}

/// <summary>分类仓储</summary>
public interface ICategoryRepository
{
    Task<List<ProductCategory>> GetAllAsync(CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<ProductCategory> categories, CancellationToken ct = default);
}

/// <summary>单位仓储</summary>
public interface IUnitRepository
{
    Task<List<ProductUnit>> GetAllAsync(CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<ProductUnit> units, CancellationToken ct = default);
}

/// <summary>桌台仓储</summary>
public interface IDeskRepository
{
    Task<List<DeskArea>> GetAreasAsync(CancellationToken ct = default);
    Task<List<Desk>> GetDesksAsync(long? areaId = null, CancellationToken ct = default);
    Task<Desk?> GetByIdAsync(long id, CancellationToken ct = default);
    Task UpdateAsync(Desk desk, CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<DeskArea> areas, IEnumerable<Desk> desks, CancellationToken ct = default);
}

/// <summary>交班仓储</summary>
public interface IShiftRepository
{
    Task<Shift?> GetCurrentAsync(CancellationToken ct = default);
    Task<Shift?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<Shift>> GetHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<long> InsertAsync(Shift shift, CancellationToken ct = default);
    Task UpdateAsync(Shift shift, CancellationToken ct = default);
}

/// <summary>离线补传队列</summary>
public interface IOutboxRepository
{
    Task<List<OutboxItem>> GetPendingAsync(int limit = 100, CancellationToken ct = default);
    Task<long> EnqueueAsync(OutboxItem item, CancellationToken ct = default);
    Task MarkSucceededAsync(long id, CancellationToken ct = default);
    Task MarkFailedAsync(long id, string error, CancellationToken ct = default);
    Task<int> CountPendingAsync(CancellationToken ct = default);
}

/// <summary>通用键值配置</summary>
public interface IDictionaryRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>数据库初始化与维护</summary>
public interface IDbInitializer
{
    /// <summary>建表（首次启动）</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>数据库文件路径</summary>
    string DatabasePath { get; }

    /// <summary>备份到指定路径</summary>
    Task BackupAsync(string targetPath, CancellationToken ct = default);
}

// ============================================================================
//  Wave 5 补充 —— 由 Lead 统一补齐
//
//  补的时机：三家的 UI 层已经用接口 + 假实现跑通了逻辑，
//  现在把契约上移到 Domain，实现类可以真正落库。
// ============================================================================

/// <summary>退款仓储</summary>
public interface IRefundRepository
{
    Task<long> InsertAsync(RefundRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<RefundRecord>> GetByOrderIdAsync(long orderId, CancellationToken ct = default);

    /// <summary>订单累计已退金额（红线校验用：累计不能超过实收）</summary>
    Task<decimal> GetTotalRefundedAsync(long orderId, CancellationToken ct = default);

    /// <summary>按班次查退款（交班对账用）</summary>
    Task<IReadOnlyList<RefundRecord>> GetByShiftIdAsync(long shiftId, CancellationToken ct = default);

    Task MarkPrintedAsync(long recordId, CancellationToken ct = default);
}

/// <summary>挂单仓储</summary>
public interface IHangOrderRepository
{
    Task<HangOrderRecord> ParkAsync(HangOrderRecord record, CancellationToken ct = default);
    Task<HangOrderRecord?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>全部挂单（默认只含 Parked）</summary>
    Task<List<HangOrderRecord>> GetAllAsync(bool includeClosed = false, CancellationToken ct = default);

    Task<List<HangOrderRecord>> GetByDeskIdAsync(long deskId, CancellationToken ct = default);
    Task RemoveAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(HangOrderRecord record, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>门店信息仓储</summary>
public interface IStoreInfoRepository
{
    Task<StoreInfo> GetAsync(CancellationToken ct = default);
    Task SaveAsync(StoreInfo info, CancellationToken ct = default);
}

/// <summary>打印机配置仓储</summary>
public interface IPrinterConfigRepository
{
    Task<List<PrinterConfig>> GetAllAsync(CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<PrinterConfig> printers, CancellationToken ct = default);
}

/// <summary>系统配置仓储</summary>
public interface ISystemConfigRepository
{
    Task<SystemConfig> GetAsync(CancellationToken ct = default);
    Task SaveAsync(SystemConfig config, CancellationToken ct = default);
}

/// <summary>收银规则仓储</summary>
public interface ICashRuleRepository
{
    Task<CashRule> GetAsync(CancellationToken ct = default);
    Task SaveAsync(CashRule rule, CancellationToken ct = default);
}
