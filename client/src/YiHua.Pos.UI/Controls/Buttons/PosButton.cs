using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace YiHua.Pos.UI.Controls.Buttons;

/// <summary>
/// 收银主按钮：Primary / Secondary / Danger 三种变体。
/// 触控目标 ≥44px，圆角 6px（输入类角色），无动画，按下即时反馈。
/// </summary>
public class PosButton : Button
{
    public static readonly StyledProperty<PosButtonVariant> VariantProperty =
        AvaloniaProperty.Register<PosButton, PosButtonVariant>(nameof(Variant), PosButtonVariant.Primary);

    /// <summary>按钮变体：Primary（唯一的承诺动作样式）/ Secondary / Danger。</summary>
    public PosButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public PosButton()
    {
        try
        {
            var styles = (Styles)AvaloniaXamlLoader.Load(
                new Uri("avares://YiHua.Pos.UI/Controls/Buttons/PosButton.axaml"));
            Styles.Add(styles);
        }
        catch
        {
            // 无头测试环境（无 Application/IAssetLoader）：应用与 XAML 等效的基础属性
            ApplyFallbackStyle();
        }
    }

    private void ApplyFallbackStyle()
    {
        MinHeight = 44;
        MinWidth = 88;
        CornerRadius = new CornerRadius(6);
        FontSize = 16;
        FontWeight = Avalonia.Media.FontWeight.SemiBold;
        Padding = new Thickness(16, 8);
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }
}

/// <summary>按钮变体。Primary 在一屏内只用于唯一的承诺动作（确定/结账）。</summary>
public enum PosButtonVariant
{
    Primary,
    Secondary,
    Danger,
}
