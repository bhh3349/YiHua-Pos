namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>文本行（支持 {变量}）</summary>
public sealed class TextElement : TemplateElement
{
    public string Text { get; set; } = string.Empty;
}
