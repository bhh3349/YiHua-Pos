using Avalonia;
using Avalonia.Controls;

namespace YiHua.Pos.UI.Controls.Buttons;

/// <summary>
/// 图标按钮：触摸友好（≥44px），用印刷符号（× + − ‹ ›）或纯文字，不用 emoji。
/// </summary>
public class IconButton : Button
{
    public static readonly StyledProperty<string> IconTextProperty =
        AvaloniaProperty.Register<IconButton, string>(nameof(IconText), string.Empty);

    /// <summary>图标文本：印刷符号如 "×" "+" "−"，禁用 emoji。</summary>
    public string IconText
    {
        get => GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public IconButton()
    {
        MinWidth = 44;
        MinHeight = 44;
        CornerRadius = new Avalonia.CornerRadius(6);
        FontSize = 20;
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Padding = new Avalonia.Thickness(8);
        this.Bind(ContentProperty, this.GetObservable(IconTextProperty));
    }
}
