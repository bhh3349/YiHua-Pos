using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace YiHua.Pos.UI.Controls.Display;

/// <summary>
/// 键值行：如「合计  128.00」「找零  12.00」。
/// 左键（次要文字）右值（主要文字），金额值用 tabular-nums。
/// </summary>
public class KeyValueRow : UserControl
{
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KeyValueRow, string>(nameof(Key), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<KeyValueRow, string>(nameof(Value), string.Empty);

    public static readonly StyledProperty<bool> IsAmountProperty =
        AvaloniaProperty.Register<KeyValueRow, bool>(nameof(IsAmount), false);

    public string Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>是否为金额（是则右值用大字号 + tabular-nums）。</summary>
    public bool IsAmount
    {
        get => GetValue(IsAmountProperty);
        set => SetValue(IsAmountProperty, value);
    }

    public KeyValueRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        var keyBlock = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        keyBlock.Bind(TextBlock.TextProperty, this.GetObservable(KeyProperty));
        keyBlock.Bind(TextBlock.FontSizeProperty, new DynamicResourceExtension("FontSizeBody"));
        keyBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextSecondaryBrush"));
        Grid.SetColumn(keyBlock, 0);

        var valueBlock = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        valueBlock.Bind(TextBlock.TextProperty, this.GetObservable(ValueProperty));
        valueBlock.Bind(TextBlock.FontSizeProperty, new DynamicResourceExtension("FontSizeBody"));
        valueBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextPrimaryBrush"));
        Grid.SetColumn(valueBlock, 1);

        // 金额值加粗 + tabular-nums
        UpdateValueStyle(valueBlock);

        grid.Children.Add(keyBlock);
        grid.Children.Add(valueBlock);
        Content = grid;
        _valueBlock = valueBlock;
    }

    private readonly TextBlock _valueBlock;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAmountProperty && _valueBlock != null)
            UpdateValueStyle(_valueBlock);
    }

    private void UpdateValueStyle(TextBlock block)
    {
        block.FontWeight = IsAmount ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
        if (IsAmount)
        {
            block.FontFeatures = new Avalonia.Media.FontFeatureCollection
            {
                new Avalonia.Media.FontFeature { Tag = "tnum", Value = 1 },
            };
        }
    }
}
