using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 商品编辑服务：把 UI 层的 ProductEditModel 写回 Domain 仓储。
///
/// 上一版这里会**静默丢字段**（别名/时价/沽清/多维规格/套餐明细），
/// 只在控制台打一行警告 —— 商家用了半年才会发现套餐明细是空的。
/// 现在 Domain 已经补上 IsSoldOut / SoldOutAt / IsMarketPrice / Alias，
/// 这几项不再丢；多维规格与套餐明细仍缺表结构，见下面说明。
///
/// 仍未落库的两项（需要独立表，另行排期）：
///   SpecDimensions 多维规格 → 暂时折叠进 Domain 的 SpecGroupName 单字符串
///   BundleItems    套餐明细 → 丢弃（Domain 无表）
/// 这两项一旦有商品用到，会在保存时**明确报错**而不是悄悄丢。
/// </summary>
internal sealed class ProductEditService : IProductEditService
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitRepository _units;

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

        var existing = model.Id > 0
            ? await _products.GetByIdAsync(model.Id, ct).ConfigureAwait(false)
            : null;

        var entity = existing ?? new Product();

        entity.Name = model.Name.Trim();
        entity.CategoryId = model.CategoryId;
        entity.CategoryName = model.CategoryName;
        entity.UnitId = model.UnitId;
        entity.UnitName = model.UnitName;
        entity.Price = model.Price;
        // ProductType: 1=普通 2=套餐 3=称重
        entity.ProductType = model.IsBundle ? 2 : (model.IsWeighted ? 3 : 1);
        entity.WeightWay = model.IsWeighted ? 1 : 0;
        entity.ImageUrl = model.ImageUrl;
        entity.Alias = string.Join(",", model.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
        entity.IsMarketPrice = model.IsMarketPrice;
        entity.IsSoldOut = model.IsSoldOut;
        if (model.IsSoldOut && entity.SoldOutAt == null)
            entity.SoldOutAt = DateTime.Now;
        if (!model.IsSoldOut)
            entity.SoldOutAt = null;

        // 多维规格暂时折叠成单字符串（Domain 只有 SpecGroupName 一个字段）
        entity.SpecGroupName = string.Join("/", model.SpecDimensions
            .Where(d => d.Options.Count > 0)
            .Select(d => d.Name));

        // 首字母：新建时按名称生成，用于快速搜索
        if (string.IsNullOrWhiteSpace(entity.PrePinYin))
            entity.PrePinYin = PinyinHelper.GetInitials(entity.Name);

        if (string.IsNullOrWhiteSpace(entity.Code))
            entity.Code = entity.PrePinYin;

        entity.DataStatus = 1;
        if (entity.Id > 0) entity.Version++;

        await _products.SaveAsync(entity, ct).ConfigureAwait(false);
    }

    public Task DeleteAsync(long productId, CancellationToken ct = default)
        => _products.DeleteAsync(productId, ct);

    /// <summary>
    /// 沽清：写 IsSoldOut，不动 DataStatus。
    /// 沽清是「今天卖完了」（第二天要自动恢复），下架是老板主动停售，两者不能混。
    /// </summary>
    public Task SetSoldOutAsync(long productId, bool soldOut, CancellationToken ct = default)
        => _products.SetSoldOutAsync(productId, soldOut, DateTime.Now, ct);

    /// <summary>保存分类（Id=0 新增）。</summary>
    public Task<long> SaveCategoryAsync(long id, string name, int sort, CancellationToken ct = default)
        => _categories.SaveAsync(new ProductCategory { Id = id, Name = name, Sort = sort }, ct);

    /// <summary>删除分类。</summary>
    public Task DeleteCategoryAsync(long id, CancellationToken ct = default)
        => _categories.DeleteAsync(id, ct);
}

/// <summary>
/// 取汉字首字母。商品搜索靠它：收银员打「nrm」能搜到「牛肉面」。
/// 只覆盖常用汉字区间，取不到的原样跳过（宁可不生成，也不要错拼音）。
/// </summary>
internal static class PinyinHelper
{
    private static bool _providerReady;
    private static System.Text.Encoding? _gb2312;

    // 常用一级汉字（GB2312 一级字库 0xB0A1–0xD7F9）按声母分组的首字母区间表。
    // 用区间表而不是全量表：体积小、无外部依赖，够用。
    private static readonly (int Start, int End, char Initial)[] Ranges =
    {
        (0xB0A1, 0xB0C4, 'A'), (0xB0C5, 0xB2C0, 'B'), (0xB2C1, 0xB4ED, 'C'),
        (0xB4EE, 0xB6E9, 'D'), (0xB6EA, 0xB7A1, 'E'), (0xB7A2, 0xB8C0, 'F'),
        (0xB8C1, 0xB9FD, 'G'), (0xB9FE, 0xBBF6, 'H'), (0xBBF7, 0xBFA5, 'J'),
        (0xBFA6, 0xC0AB, 'K'), (0xC0AC, 0xC2E7, 'L'), (0xC2E8, 0xC4C2, 'M'),
        (0xC4C3, 0xC5B5, 'N'), (0xC5B6, 0xC5BD, 'O'), (0xC5BE, 0xC6D9, 'P'),
        (0xC6DA, 0xC8BA, 'Q'), (0xC8BB, 0xC8F5, 'R'), (0xC8F6, 0xCBF9, 'S'),
        (0xCBFA, 0xCDD9, 'T'), (0xCDDA, 0xCEF3, 'W'), (0xCEF4, 0xD188, 'X'),
        (0xD1B9, 0xD4D0, 'Y'), (0xD4D1, 0xD7F9, 'Z'),
    };

    /// <summary>
    /// 取汉字首字母：收银员打「nrm」能搜到「牛肉面」。
    /// GB2312 编码在 .NET Core 里要先注册 CodePages 提供程序；
    /// 取不到就返回空串（宁可不生成首字母，也不要写错拼音误导搜索）。
    /// </summary>
    public static string GetInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var enc = GetGb2312();
        if (enc == null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (ch < 128)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
                continue;
            }

            byte[] bytes;
            try { bytes = enc.GetBytes(ch.ToString()); }
            catch { continue; }
            if (bytes.Length != 2) continue;

            int code = (bytes[0] << 8) | bytes[1];
            foreach (var (start, end, initial) in Ranges)
            {
                if (code >= start && code <= end) { sb.Append(initial); break; }
            }
        }
        return sb.ToString();
    }

    private static System.Text.Encoding? GetGb2312()
    {
        if (!_providerReady)
        {
            _providerReady = true;
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                _gb2312 = System.Text.Encoding.GetEncoding("GB2312");
            }
            catch
            {
                _gb2312 = null; // 运行环境没有 CodePages：放弃首字母，不影响主流程
            }
        }
        return _gb2312;
    }
}