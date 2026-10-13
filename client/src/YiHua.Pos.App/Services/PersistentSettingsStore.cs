using System.Text.Json;
using SqlSugar;
using YiHua.Pos.Data;
using YiHua.Pos.Data.Repositories;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Settings;

namespace YiHua.Pos.App.Services;

/// <summary>
/// ISettingsStore 的正式实现：所有设置写入 DictEntry，重启后保留。
/// 之前接的是 InMemorySettingsStore，设置改完退出软件就消失。
/// </summary>
internal sealed class PersistentSettingsStore : ISettingsStore
{
    private readonly ISqlSugarClient _db;

    public PersistentSettingsStore(PosDbContext context) => _db = context.Db;

    public async Task<StoreInfoDto> GetStoreInfoAsync(CancellationToken ct = default)
    {
        var entity = await new StoreInfoRepository(_db).GetAsync(ct).ConfigureAwait(false);
        return new StoreInfoDto
        {
            StoreName = entity.StoreName,
            Address = entity.Address,
            Phone = entity.Phone,
            LogoData = entity.Logo,
        };
    }

    public Task SaveStoreInfoAsync(StoreInfoDto info, CancellationToken ct = default)
        => new StoreInfoRepository(_db).SaveAsync(new StoreInfo
        {
            StoreName = info.StoreName,
            Address = info.Address,
            Phone = info.Phone,
            Logo = info.LogoData,
        }, ct);

    public async Task<List<PrinterConfigDto>> GetPrintersAsync(CancellationToken ct = default)
    {
        var list = await new PrinterConfigRepository(_db).GetAllAsync(ct).ConfigureAwait(false);
        return list.Select(x => new PrinterConfigDto
        {
            Id = x.Id.ToString(),
            Name = x.Name,
            PaperWidth = x.PaperWidthMm,
            Usage = MapUsage(x.Usage),
            DevicePath = x.DevicePath,
            Enabled = x.Enabled,
            Station = x.Station,
        }).ToList();
    }

    public Task SavePrintersAsync(List<PrinterConfigDto> printers, CancellationToken ct = default)
        => new PrinterConfigRepository(_db).ReplaceAllAsync(printers.Select(x => new PrinterConfig
        {
            Id = long.TryParse(x.Id, out var id) ? id : 0,
            Name = x.Name,
            PaperWidthMm = x.PaperWidth,
            Usage = MapUsage(x.Usage),
            DevicePath = x.DevicePath,
            Enabled = x.Enabled,
            Station = x.Station,
            Connection = x.DevicePath.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase)
                ? PrinterConnection.Network
                : x.DevicePath.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    ? PrinterConnection.Serial : PrinterConnection.Usb,
        }), ct);

    public async Task<CashRuleDto> GetCashRuleAsync(CancellationToken ct = default)
    {
        var e = await new CashRuleRepository(_db).GetAsync(ct).ConfigureAwait(false);
        return new CashRuleDto
        {
            RoundingRule = e.Rounding,
            AllowItemDiscount = e.AllowItemDiscount,
            AllowOrderDiscount = e.AllowOrderDiscount,
            AllowMemberDiscount = e.AllowMemberDiscount,
            PrintItems = e.PrintItems,
            PrintDiscount = e.PrintDiscount,
        };
    }

    public Task SaveCashRuleAsync(CashRuleDto rule, CancellationToken ct = default)
        => new CashRuleRepository(_db).SaveAsync(new CashRule
        {
            Rounding = rule.RoundingRule,
            AllowItemDiscount = rule.AllowItemDiscount,
            AllowOrderDiscount = rule.AllowOrderDiscount,
            AllowMemberDiscount = rule.AllowMemberDiscount,
            PrintItems = rule.PrintItems,
            PrintDiscount = rule.PrintDiscount,
        }, ct);

    public async Task<SyncInfoDto> GetSyncInfoAsync(CancellationToken ct = default)
    {
        var e = await new SystemConfigRepository(_db).GetAsync(ct).ConfigureAwait(false);
        return new SyncInfoDto
        {
            ServerAddress = string.IsNullOrWhiteSpace(e.ServerUrl) ? "http://localhost:8000" : e.ServerUrl,
            IsOnline = !e.OfflineMode,
        };
    }

    public Task SaveSyncInfoAsync(SyncInfoDto info, CancellationToken ct = default)
        => new SystemConfigRepository(_db).SaveAsync(new SystemConfig
        {
            OfflineMode = !info.IsOnline,
            ServerUrl = info.ServerAddress,
        }, ct);

    private static PrinterUsage MapUsage(PrinterUsage x) => x;
}