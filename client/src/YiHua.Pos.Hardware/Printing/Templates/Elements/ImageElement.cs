namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>图片（logo）</summary>
public sealed class ImageElement : TemplateElement
{
    /// <summary>PNG 字节（超宽时等比缩小到纸宽点数）</summary>
    public byte[]? PngData { get; set; }
}
