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

    public int Sort { get; set; }

    public long Version { get; set; } = 1;

    public bool IsOnSale => DataStatus == 1;
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
