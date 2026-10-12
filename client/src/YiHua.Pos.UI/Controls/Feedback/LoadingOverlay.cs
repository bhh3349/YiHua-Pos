using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Feedback;

/// <summary>
/// 加载遮罩：半透明覆盖 + "加载中"文字，无动画（高频场景不拖累）。
/// </summary>
public class LoadingOverlay : Grid
{
    public static readonly StyledProperty<string> LoadingTextProperty =
        AvaloniaProperty.Register<LoadingOverlay, string>(nameof(LoadingText), "加载中…");

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LoadingOverlay, bool>(nameof(IsLoading));

    public string LoadingText
    {
        get => GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public LoadingOverlay()
    {
        Background = new SolidColorBrush(Colors.White, 0.7);
        var label = new TextBlock
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 16,
        };
        label.Bind(TextBlock.TextProperty, this.GetObservable(LoadingTextProperty));
        label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextSecondaryBrush"));
        Children.Add(label);
        this.Bind(IsVisibleProperty, this.GetObservable(IsLoadingProperty));
    }
}
