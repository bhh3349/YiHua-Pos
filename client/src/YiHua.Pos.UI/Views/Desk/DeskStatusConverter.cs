using System.Globalization;
using Avalonia.Data.Converters;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Controls.Cards;

namespace YiHua.Pos.UI.Views.Desk;

/// <summary>Domain DeskStatus → DeskCardStatus 转换器</summary>
public class DeskStatusConverter : IValueConverter
{
    public static readonly DeskStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DeskStatus s ? s switch
        {
            DeskStatus.Occupied => DeskCardStatus.Occupied,
            DeskStatus.Reserved => DeskCardStatus.Reserved,
            _ => DeskCardStatus.Idle,
        } : DeskCardStatus.Idle;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
