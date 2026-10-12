namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>二维码</summary>
public sealed class QrCodeElement : TemplateElement
{
    /// <summary>内容（支持 {变量}）</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>模块尺寸 1~16，默认 6</summary>
    public int Size { get; set; } = 6;
}
