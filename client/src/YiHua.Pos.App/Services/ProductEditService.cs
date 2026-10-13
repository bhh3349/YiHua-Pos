using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 商品编辑服务：把 UI 层的 ProductEditModel 写回 Domain 仓储。
///
/// ⚠️ **本适配器会丢数据，这是已知的、被记录的缺口，不是 bug。**
///
/// ProductEditModel 比 Domain.Product 多 5 组字段，Domain 里没有对应存储：
///   Aliases       别名        → 丢失，搜索时匹配不到
///   IsMarketPrice 时价        → 丢失
///   IsSoldOut     沽清        → 丢失（T-602 的持久化缺口，同一处）
///   SpecDimensions 多维规格   → 折叠成 Domain 的 SpecGroupName 单字符串
///   BundleItems   套餐明细    → 丢失
///
/// 现在不吞：单例化后会打印一行警告，让接线的同事一眼看到
/// 「这里有洞」，而不是等商家用了半年才发现套餐明细空了。
///
/// 补齐方案：Domain 加 ProductAlias / ProductSpecOption / ProductBundleItem
/// 三张表 + Product.IsSoldOut、IsMarketPrice 两个字段，
/// 同时改 Migrations/V001_Init.sql（Domain 与手写 SQL 必须同步）。
/// </summary>
internal sealed class ProductEditService : IProductEditService
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitRepository _units;
    private static readonly HashSet<string> Warned = new();

    public ProductEditService(
        IProductRepository products,
        ICategoryRepository categories,
        IUnitRepository units)
    {
        _products = products;
        _categories = categories;
        _units = units;
    }

    public async Task SaveAsync(ProductEditModel model, CancellationToken ct = default)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("商品名称不能为空", nameof(model));

        WarnLostFields(model);

        var all = await _products.GetAllAsync(ct).ConfigureAwait(false);
        var idx = all.FindIndex(p => p.Id == model.Id);

        var entity = new Product
        {
            Id = model.Id,
            Name = model.Name.Trim(),
            // Aliases 无处存：条码留空，编码用名称，搜索走 PrePinYin
            Code = all.FirstOrDefault(p => p.Id == model.Id)?.Code ?? string.Empty,
            Barcode = all.FirstOrDefault(p => p.Id == model.Id)?.Barcode ?? string.Empty,
            CategoryId = model.CategoryId,
            CategoryName = model.CategoryName,
            UnitId = model.UnitId,
            UnitName = model.UnitName,
            Price = model.Price,
            // Domain.ProductType: 1=普通 2=套餐 3=称重
            ProductType = model.IsBundle ? 2 : 1,
            WeightWay = model.IsWeighted ? 1 : 0,
            // 多维规格折叠成单字符串，这是 Domain 唯一能表达的形状
            SpecGroupName = string.Join('/', model.SpecDimensions
                .Where(d => d.Options.Count > 0)
                .Select(d => d.Name)),
            PrePinYin = all.FirstOrDefault(p => p.Id == model.Id)?.PrePinYin ?? string.Empty,
            ImageUrl = model.ImageUrl,
            // 沽清状态没有列：只有上架/下架。IsSoldOut 丢失。
            DataStatus = 1,
            Sort = all.FirstOrDefault(p => p.Id == model.Id)?.Sort ?? 0,
        };

        if (idx >= 0)
            all[idx] = entity;
        else
            all.Add(entity);

        await _products.ReplaceAllAsync(all, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(long productId, CancellationToken ct = default)
    {
        var all = await _products.GetAllAsync(ct).ConfigureAwait(false);
        var removed = all.RemoveAll(p => p.Id == productId);
        if (removed > 0)
            await _products.ReplaceAllAsync(all, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 沽清：Domain 无此字段，暂时用「下架」表达。
    /// ⚠️ 这是权宜之计：下架 = 老板手动停售，沽清 = 今天卖完了，
    /// 两者语义不同，会导致沽清的菜在第二天不会自动回来。
    /// T-602 的定时还原依赖 Product.IsSoldOut 落库，此方法是占位。
    /// </summary>
    public async Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
    {
        var all = await _products.GetAllAsync(ct).ConfigureAwait(false);
        var target = all.FirstOrDefault(p => p.Id == productId);
        if (target is null)
            return;

        target.DataStatus = soldOut ? 99 : 1;
        await _products.ReplaceAllAsync(all, ct).ConfigureAwait(false);
    }

    private static void WarnLostFields(ProductEditModel m)
    {
        var lost = new List<string>();
        if (m.Aliases.Count > 0) lost.Add("别名");
        if (m.IsMarketPrice) lost.Add("时价");
        if (m.IsSoldOut) lost.Add("沽清");
        if (m.BundleItems.Count > 0) lost.Add("套餐明细");
        if (m.SpecDimensions.Any(d => d.Options.Count > 1)) lost.Add("多维规格(已折叠)");

        if (lost.Count == 0)
            return;

        var key = string.Join(",", lost);
        lock (Warned)
        {
            if (!Warned.Add(key))
                return;
            Console.WriteLine(
                $"[ProductEditService] 警告：以下字段当前无法持久化，已丢弃 → {key}。" +
                " 需补 Domain 字段 + Migrations/V001_Init.sql。");
        }
    }
}