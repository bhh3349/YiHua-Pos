using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace YiHua.Pos.UI.Controls.Cards;

/// <summary>
/// 分类切换标签：横向排列的分类入口，选中态用品牌蓝底白字。
/// </summary>
public partial class CategoryTab : UserControl
{
    public static readonly StyledProperty<string> CategoryNameProperty =
        AvaloniaProperty.Register<CategoryTab, string>(nameof(CategoryName), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<CategoryTab, bool>(nameof(IsSelected));

    public string CategoryName
    {
        get => GetValue(CategoryNameProperty);
        set => SetValue(CategoryNameProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>点击分类时触发。</summary>
    public event EventHandler? TabClicked;

    public CategoryTab()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateSelectedVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
            UpdateSelectedVisual();
    }

    private void UpdateSelectedVisual()
    {
        var btn = this.FindControl<Button>("TabButton");
        var txt = this.FindControl<TextBlock>("TabText");
        if (btn == null || txt == null) return;
        // 选中：品牌蓝底白字；未选中：透明底次要文字
        if (IsSelected)
        {
            btn.Bind(Button.BackgroundProperty, new DynamicResourceExtension("PrimaryBrush"));
            txt.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextOnPrimaryBrush"));
        }
        else
        {
            btn.ClearValue(Button.BackgroundProperty);
            txt.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextSecondaryBrush"));
        }
        txt.FontWeight = IsSelected ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
    }

    private void OnTabClick(object? sender, RoutedEventArgs e) => TabClicked?.Invoke(this, EventArgs.Empty);
}
