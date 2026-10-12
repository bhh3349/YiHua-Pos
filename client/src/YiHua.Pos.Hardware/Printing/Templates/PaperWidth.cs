namespace YiHua.Pos.Hardware.Printing.Templates;

/// <summary>纸张规格</summary>
public enum PaperWidth
{
    Mm58 = 58,
    Mm80 = 80,
}

/// <summary>纸张规格扩展：每行字符数（ASCII 基准，默认字号）</summary>
public static class PaperWidthExtensions
{
    /// <summary>58mm 纸 32 列，80mm 纸 48 列</summary>
    public static int CharsPerLine(this PaperWidth paper) => paper == PaperWidth.Mm58 ? 32 : 48;
}
