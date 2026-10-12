namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>分隔线（按纸宽自动生成）</summary>
public sealed class LineElement : TemplateElement
{
    public char Char { get; set; } = '-';
}
