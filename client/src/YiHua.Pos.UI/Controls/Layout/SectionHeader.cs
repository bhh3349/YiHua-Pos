using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace YiHua.Pos.UI.Controls.Layout;

/// <summary>
/// 区块标题：小字号次要文字 + 可选右侧操作，不抢视觉。
/// </summary>
public class SectionHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SectionHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<SectionHeader, object?>(nameof(Action));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>右侧操作区（可选）。</summary>
    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public SectionHeader()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        title.Bind(TextBlock.TextProperty, this.GetObservable(TitleProperty));
        title.Bind(TextBlock.FontSizeProperty, new DynamicResourceExtension("FontSizeSmall"));
        title.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextSecondaryBrush"));
        title.FontWeight = Avalonia.Media.FontWeight.SemiBold;
        Grid.SetColumn(title, 0);
        var actionHost = new Avalonia.Controls.Presenters.ContentPresenter { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        actionHost.Bind(Avalonia.Controls.Presenters.ContentPresenter.ContentProperty, this.GetObservable(ActionProperty));
        Grid.SetColumn(actionHost, 1);
        grid.Children.Add(title);
        grid.Children.Add(actionHost);
        Content = grid;
    }
}
