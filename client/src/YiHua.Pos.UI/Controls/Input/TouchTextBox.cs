using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace YiHua.Pos.UI.Controls.Input;

/// <summary>
/// 触摸友好输入框：大热区（≥48px 高）、大字号（16px）、圆角 6px。
/// 基于 TextBox，预设收银场景的触摸参数。
/// </summary>
public class TouchTextBox : TextBox
{
    public TouchTextBox()
    {
        MinHeight = 48;
        CornerRadius = new Avalonia.CornerRadius(6);
        this.Bind(FontSizeProperty, new DynamicResourceExtension("FontSizeBody"));
        Padding = new Avalonia.Thickness(12, 8);
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }
}
