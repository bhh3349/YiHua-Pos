namespace YiHua.Pos.Hardware.Printing.EscPos;

/// <summary>对齐方式（ESC a n）</summary>
public enum AlignStyle
{
    /// <summary>左对齐</summary>
    Left = 0,

    /// <summary>居中</summary>
    Center = 1,

    /// <summary>右对齐</summary>
    Right = 2,
}

/// <summary>字符集</summary>
public enum CharsetType
{
    /// <summary>西文 ASCII（退出中文模式）</summary>
    Ascii,

    /// <summary>中文 GB18030（进入中文模式）</summary>
    Gb18030,
}

/// <summary>切纸模式（GS V m）</summary>
public enum CutMode
{
    /// <summary>全切</summary>
    Full = 0,

    /// <summary>半切（留一点不断开）</summary>
    Partial = 1,
}

/// <summary>一维码类型（GS k m）</summary>
public enum BarcodeType
{
    /// <summary>UPC-A</summary>
    UpcA = 0,

    /// <summary>UPC-E</summary>
    UpcE = 1,

    /// <summary>EAN-13</summary>
    Ean13 = 2,

    /// <summary>EAN-8</summary>
    Ean8 = 3,

    /// <summary>CODE39</summary>
    Code39 = 4,

    /// <summary>ITF（交叉二五码）</summary>
    Itf = 5,

    /// <summary>CODABAR</summary>
    Codabar = 6,

    /// <summary>CODE93</summary>
    Code93 = 72,

    /// <summary>CODE128</summary>
    Code128 = 73,
}
