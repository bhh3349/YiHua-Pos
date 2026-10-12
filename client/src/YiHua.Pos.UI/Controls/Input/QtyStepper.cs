using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Controls.Input;

/// <summary>
/// 数量加减器：大触控按钮 + 数量显示，改价/改量的高频入口。
/// 按键 48px，无动画。
/// </summary>
public partial class QtyStepper : UserControl
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<QtyStepper, decimal>(nameof(Value), 1m);

    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<QtyStepper, decimal>(nameof(Minimum), 0m);

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<QtyStepper, decimal>(nameof(Maximum), 999m);

    public static readonly StyledProperty<decimal> StepProperty =
        AvaloniaProperty.Register<QtyStepper, decimal>(nameof(Step), 1m);

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Coerce(value));
    }

    public decimal Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>值变化时触发。</summary>
    public event EventHandler<decimal>? ValueChanged;

    public QtyStepper()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private decimal Coerce(decimal v) => Math.Max(Minimum, Math.Min(Maximum, v));

    /// <summary>增加（可测试）。</summary>
    public void Increment()
    {
        Value = Coerce(Value + Step);
        ValueChanged?.Invoke(this, Value);
    }

    /// <summary>减少（可测试）。</summary>
    public void Decrement()
    {
        Value = Coerce(Value - Step);
        ValueChanged?.Invoke(this, Value);
    }

    private void OnMinusClick(object? sender, RoutedEventArgs e) => Decrement();
    private void OnPlusClick(object? sender, RoutedEventArgs e) => Increment();
}
