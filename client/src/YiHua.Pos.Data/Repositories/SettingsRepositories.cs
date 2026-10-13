using System.Text.Json;
using SqlSugar;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.Data.Repositories;

/// <summary>门店配置持久化。</summary>
public sealed class StoreInfoRepository : IStoreInfoRepository
{
    private readonly ISqlSugarClient _db;
    public StoreInfoRepository(ISqlSugarClient db) => _db = db;

    public async Task<StoreInfo> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.Queryable<DictEntry>().Where(x => x.Key == "settings:store").FirstAsync();
        return row == null ? new StoreInfo() : JsonSerializer.Deserialize<StoreInfo>(row.Value) ?? new StoreInfo();
    }

    public Task SaveAsync(StoreInfo info, CancellationToken ct = default)
        => Set("settings:store", JsonSerializer.Serialize(info));

    private Task Set(string key, string value) => new DictionaryRepository(_db).SetAsync(key, value);
}

/// <summary>打印机配置持久化。</summary>
public sealed class PrinterConfigRepository : IPrinterConfigRepository
{
    private readonly ISqlSugarClient _db;
    public PrinterConfigRepository(ISqlSugarClient db) => _db = db;

    public async Task<List<PrinterConfig>> GetAllAsync(CancellationToken ct = default)
    {
        var row = await _db.Queryable<DictEntry>().Where(x => x.Key == "settings:printers").FirstAsync();
        return row == null ? new List<PrinterConfig>() : JsonSerializer.Deserialize<List<PrinterConfig>>(row.Value) ?? new List<PrinterConfig>();
    }

    public Task ReplaceAllAsync(IEnumerable<PrinterConfig> printers, CancellationToken ct = default)
        => new DictionaryRepository(_db).SetAsync("settings:printers", JsonSerializer.Serialize(printers));
}

/// <summary>系统配置持久化。</summary>
public sealed class SystemConfigRepository : ISystemConfigRepository
{
    private readonly ISqlSugarClient _db;
    public SystemConfigRepository(ISqlSugarClient db) => _db = db;

    public async Task<SystemConfig> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.Queryable<DictEntry>().Where(x => x.Key == "settings:system").FirstAsync();
        return row == null ? new SystemConfig() : JsonSerializer.Deserialize<SystemConfig>(row.Value) ?? new SystemConfig();
    }

    public Task SaveAsync(SystemConfig config, CancellationToken ct = default)
        => new DictionaryRepository(_db).SetAsync("settings:system", JsonSerializer.Serialize(config));
}

/// <summary>收银规则持久化。</summary>
public sealed class CashRuleRepository : ICashRuleRepository
{
    private readonly ISqlSugarClient _db;
    public CashRuleRepository(ISqlSugarClient db) => _db = db;

    public async Task<CashRule> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.Queryable<DictEntry>().Where(x => x.Key == "settings:cash-rule").FirstAsync();
        return row == null ? new CashRule() : JsonSerializer.Deserialize<CashRule>(row.Value) ?? new CashRule();
    }

    public Task SaveAsync(CashRule rule, CancellationToken ct = default)
        => new DictionaryRepository(_db).SetAsync("settings:cash-rule", JsonSerializer.Serialize(rule));
}
