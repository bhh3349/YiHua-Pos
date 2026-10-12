using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Display;

/// <summary>
/// 状态标签：克制的微标签（非高饱和彩色胶囊）。
/// 浅底（12% 透明度）+ 深色文字，不抢夺注意力。
/// </summary>
public class StatusTag : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<StatusTag, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<StatusTagKind> KindProperty =
        AvaloniaProperty.Register<StatusTag, StatusTagKind>(nameof(Kind), StatusTagKind.Neutral);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusTagKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private readonly TextBlock _label;

    public StatusTag()
    {
        CornerRadius = new CornerRadius(4); // 微标签用更小圆角（角色区分）
        Padding = new Thickness(8, 4);
        _label = new TextBlock { FontSize = 12 };
        _label.Bind(TextBlock.TextProperty, this.GetObservable(TextProperty));
        Child = _label;
        UpdateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
            UpdateVisual();
    }

    private void UpdateVisual()
    {
        var (colorKey, brushKey) = GetStyle(Kind);
        // 浅底色：用对应颜色 12% 透明度的派生刷
        var brush = new SolidColorBrush(Colors.Gray, 0.12);
        brush.Bind(SolidColorBrush.ColorProperty, new DynamicResourceExtension(colorKey));
        Background = brush;
        _label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(brushKey));
    }

    /// <summary>根据 Kind 计算样式键（可测试的纯逻辑）。</summary>
    public static (string ColorKey, string BrushKey) GetStyle(StatusTagKind kind) => kind switch
    {
        StatusTagKind.Success => ("SuccessColor", "SuccessBrush"),
        StatusTagKind.Warning => ("WarningColor", "WarningBrush"),
        StatusTagKind.Danger => ("DangerColor", "DangerBrush"),
        StatusTagKind.Info => ("InfoColor", "InfoBrush"),
        _ => ("TextSecondaryColor", "TextSecondaryBrush"),
    };
}

public enum StatusTagKind
{
    Neutral,
    Success,
    Warning,
    Danger,
    Info,
}
