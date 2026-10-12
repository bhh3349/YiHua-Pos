namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>空白行</summary>
public sealed class SpacerElement : TemplateElement
{
    /// <summary>空行数，默认 1</summary>
    public int Lines { get; set; } = 1;
}
