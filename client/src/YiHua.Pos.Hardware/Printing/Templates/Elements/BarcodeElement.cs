using YiHua.Pos.Hardware.Printing.EscPos;

namespace YiHua.Pos.Hardware.Printing.Templates.Elements;

/// <summary>一维码</summary>
public sealed class BarcodeElement : TemplateElement
{
    /// <summary>内容（支持 {变量}，ASCII）</summary>
    public string Content { get; set; } = string.Empty;
    public BarcodeType Type { get; set; } = BarcodeType.Code128;
}
