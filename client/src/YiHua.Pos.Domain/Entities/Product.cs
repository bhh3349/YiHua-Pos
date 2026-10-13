using YiHua.Pos.Domain.Enums;

namespace YiHua.Pos.Domain.Entities;

/// <summary>商品</summary>
public class Product
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>商品编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>条码</summary>
    public string Barcode { get; set; } = string.Empty;

    public long CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public long UnitId { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal MemberPrice { get; set; }

    /// <summary>1=普通 2=套餐 3=称重</summary>
    public int ProductType { get; set; } = 1;

    /// <summary>称重方式：0=不计重 1=计重</summary>
    public int WeightWay { get; set; }

    /// <summary>规格组名（如「辣度」），空表示无规格</summary>
    public string SpecGroupName { get; set; } = string.Empty;

    /// <summary>名称首字母，用于快速搜索</summary>
    public string PrePinYin { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>1=上架 99=下架</summary>
    public int DataStatus { get; set; } = 1;

    /// <summary>
    /// 沽清（今天卖完了，可逆）。
    ///
    /// 与 DataStatus 是两件事，不能合并：
    ///   DataStatus=99 是老板主动下架（可能长期不卖了）；
    ///   IsSoldOut 是今天备料用完了，第二天要自动恢复。
    /// 之前用 DataStatus 顶替，导致沽清的菜第二天不会自己回来。
    /// </summary>
    public bool IsSoldOut { get; set; }

    /// <summary>沽清时间（用于「定时自动还原」判断是否隔天）</summary>
    public DateTime? SoldOutAt { get; set; }

    /// <summary>时价：每天开市前确认当日价格（如海鲜）</summary>
    public bool IsMarketPrice { get; set; }

    /// <summary>
    /// 别名，逗号分隔（如「牛肉面,牛肉拉面」）。
    /// 用于快速搜索：顾客/收银员说「牛肉面」，店里菜单写的是「兰州牛肉面(小)」。
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    public int Sort { get; set; }

    public long Version { get; set; } = 1;

    public bool IsOnSale => DataStatus == 1;

    /// <summary>可点：已上架且未沽清</summary>
    public bool CanOrder => DataStatus == 1 && !IsSoldOut;
}

/// <summary>商品分类</summary>
public class ProductCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>1=基础分类 0=营销分类</summary>
    public int CategoryType { get; set; } = 1;

    public int Sort { get; set; }
    public int DataStatus { get; set; } = 1;
    public long Version { get; set; } = 1;
}

/// <summary>计量单位</summary>
public class ProductUnit
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Sort { get; set; }
    public long Version { get; set; } = 1;
}
