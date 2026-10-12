using YiHua.Pos.Hardware.Printing.EscPos;

namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>打印模板</summary>
public sealed class PrintTemplate
{
    public string Name { get; set; } = string.Empty;
    public PaperWidth Paper { get; set; } = PaperWidth.Mm80;
    public List<TemplateElement> Elements { get; set; } = new();
    /// <summary>渲染结束后是否走纸并切纸，默认 true</summary>
    public bool CutAfter { get; set; } = true;
}

/// <summary>模板元素基类</summary>
public abstract class TemplateElement
{
    public AlignStyle Align { get; set; } = AlignStyle.Left;
    public bool Bold { get; set; }
    /// <summary>字宽倍数 1~8</summary>
    public int FontWidth { get; set; } = 1;
    /// <summary>字高倍数 1~8</summary>
    public int FontHeight { get; set; } = 1;
}
