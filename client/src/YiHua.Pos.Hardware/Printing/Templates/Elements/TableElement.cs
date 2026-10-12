namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>表格（商品明细）</summary>
public sealed class TableElement : TemplateElement
{
    public string[] Columns { get; set; } = Array.Empty<string>();
    /// <summary>每列字符宽度（显示宽度，中文按 2 计）</summary>
    public int[] Widths { get; set; } = Array.Empty<int>();
    /// <summary>数据源变量名，如 "items"（上下文中应为 IEnumerable&lt;PrintItemRow&gt;）</summary>
    public string DataSource { get; set; } = string.Empty;
    /// <summary>
    /// 列到 PrintItemRow 属性的映射，如 ["Name","Qty","Amount"]。
    /// 可用属性：Name / Spec / Qty / Price / Amount / Remark。为空时默认 ["Name","Qty","Amount"]。
    /// </summary>
    public string[] Fields { get; set; } = Array.Empty<string>();
    /// <summary>是否打印表头，默认 true</summary>
    public bool ShowHeader { get; set; } = true;
}

/// <summary>商品明细行（表格数据源的行模型）</summary>
public sealed class PrintItemRow
{
    public string Name { get; set; } = string.Empty;
    public string Spec { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public string Remark { get; set; } = string.Empty;
}
