using System.Globalization;
using Avalonia.Data.Converters;
using YiHua.Pos.UI.Controls.Cards;

namespace YiHua.Pos.UI.Controls.Converters;

/// <summary>商品状态 → 标签文本。</summary>
public class ProductStateTagConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProductCardState s ? ProductCard.GetStateTag(s) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>商品状态 → 是否显示标签。</summary>
public class ProductHasTagConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProductCardState s && ProductCard.HasStateTag(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
