using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Display;

/// <summary>
/// 金额显示（签名控件）：
/// 整数部分巨大（40px）、小数部分降级（20px），均 tabular-nums（tnum），
/// 从视觉上消灭"看错钱"。小数位数固定，不足补零，避免跳动。
/// </summary>
public class AmountDisplay : UserControl
{
    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<AmountDisplay, decimal>(nameof(Amount));

    public static readonly StyledProperty<int> DecimalPlacesProperty =
        AvaloniaProperty.Register<AmountDisplay, int>(nameof(DecimalPlaces), 2);

    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<AmountDisplay, string>(nameof(CurrencySymbol), "¥");

    /// <summary>金额值。</summary>
    public decimal Amount
    {
        get => GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    /// <summary>小数位数（默认 2）。</summary>
    public int DecimalPlaces
    {
        get => GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    /// <summary>货币符号（默认 ¥）。</summary>
    public string CurrencySymbol
    {
        get => GetValue(CurrencySymbolProperty);
        set => SetValue(CurrencySymbolProperty, value);
    }

    private readonly TextBlock _symbolBlock;
    private readonly TextBlock _integerBlock;
    private readonly TextBlock _decimalBlock;

    public AmountDisplay()
    {
        // 布局：符号 + 整数（大）+ 小数（小），基线对齐
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        _symbolBlock = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        _symbolBlock.Bind(TextBlock.FontSizeProperty,
            new DynamicResourceExtension("FontSizeSubtitle"));
        _symbolBlock.Bind(TextBlock.ForegroundProperty,
            new DynamicResourceExtension("TextSecondaryBrush"));

        _integerBlock = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
        };
        _integerBlock.Bind(TextBlock.FontSizeProperty,
            new DynamicResourceExtension("FontSizeAmount"));
        _integerBlock.Bind(TextBlock.ForegroundProperty,
            new DynamicResourceExtension("TextPrimaryBrush"));
        ApplyTabularNums(_integerBlock);

        _decimalBlock = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
        };
        _decimalBlock.Bind(TextBlock.FontSizeProperty,
            new DynamicResourceExtension("FontSizeSubtitle"));
        _decimalBlock.Bind(TextBlock.ForegroundProperty,
            new DynamicResourceExtension("TextSecondaryBrush"));
        ApplyTabularNums(_decimalBlock);

        panel.Children.Add(_symbolBlock);
        panel.Children.Add(_integerBlock);
        panel.Children.Add(_decimalBlock);
        Content = panel;

        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AmountProperty ||
            change.Property == DecimalPlacesProperty ||
            change.Property == CurrencySymbolProperty)
        {
            UpdateText();
        }
    }

    private void UpdateText()
    {
        var (integer, fraction) = SplitAmount(Amount, DecimalPlaces);
        _symbolBlock.Text = CurrencySymbol;
        _integerBlock.Text = integer;
        _decimalBlock.Text = string.IsNullOrEmpty(fraction) ? string.Empty : "." + fraction;
    }

    /// <summary>
    /// 拆分金额为整数/小数部分（可测试的纯逻辑）。
    /// 小数固定位数，不足补零；负数符号归整数部分。
    /// </summary>
    public static (string Integer, string Fraction) SplitAmount(decimal amount, int decimalPlaces)
    {
        if (decimalPlaces < 0) decimalPlaces = 0;
        var sign = amount < 0 ? "-" : "";
        var abs = Math.Abs(amount);
        var integer = sign + Math.Truncate(abs).ToString("N0");
        var fraction = decimalPlaces == 0
            ? string.Empty
            : (abs - Math.Truncate(abs)).ToString("F" + decimalPlaces).Substring(2);
        // 处理四舍五入进位（如 9.999 → 10.00）
        // 必须显式指定 AwayFromZero：.NET 默认是 ToEven（银行家舍入），
        // 那样 9.995 会变成 9.99，收银金额差一分钱。
        var rounded = Math.Round(abs, decimalPlaces, MidpointRounding.AwayFromZero);
        integer = sign + Math.Truncate(rounded).ToString("N0");
        fraction = decimalPlaces == 0
            ? string.Empty
            : (rounded - Math.Truncate(rounded)).ToString("F" + decimalPlaces).Substring(2).PadRight(decimalPlaces, '0');
        return (integer, fraction);
    }

    private static void ApplyTabularNums(TextBlock block)
    {
        // tnum：等宽数字，防止金额跳动
        block.FontFeatures = new FontFeatureCollection
        {
            new FontFeature { Tag = "tnum", Value = 1 },
        };
    }
}
