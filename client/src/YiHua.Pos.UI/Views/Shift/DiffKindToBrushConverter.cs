using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using YiHua.Pos.UI.Services.Shift;

namespace YiHua.Pos.UI.Views.Shift;

/// <summary>
/// 差额类型 → 背景刷：短款用警告色，溢余用中性色，持平用成功色底。
/// 不一律红色。
/// </summary>
public class DiffKindToBrushConverter : IValueConverter
{
    public static readonly DiffKindToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 返回资源键，由 XAML 的 DynamicResource 解析；此处返回 Brush 占位
        // 实际使用时在 XAML Resources 中定义对应 Brush
        return value is DifferenceKind kind ? kind switch
        {
            DifferenceKind.Shortage => new SolidColorBrush(Color.Parse("#FFF3E0")), // 警告浅底
            DifferenceKind.Overage => new SolidColorBrush(Color.Parse("#F5F5F5")),   // 中性浅底
            _ => new SolidColorBrush(Color.Parse("#E8F5E9")),                        // 持平浅绿底
        } : new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
