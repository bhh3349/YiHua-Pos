using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Controls.Input;

/// <summary>
/// 数字键盘（收银必备）。
/// 按键 ≥56px，高频操作无动画，按下即时反馈（≤100ms）。
/// 确定键是全键盘唯一的 Primary 实心键（承诺动作）。
/// 输入逻辑为纯方法，可无头测试。
/// </summary>
public partial class NumberPad : UserControl
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<NumberPad, decimal>(nameof(Value));

    public static readonly StyledProperty<decimal> MaxValueProperty =
        AvaloniaProperty.Register<NumberPad, decimal>(nameof(MaxValue), 999999.99m);

    public static readonly StyledProperty<int> MaxDecimalPlacesProperty =
        AvaloniaProperty.Register<NumberPad, int>(nameof(MaxDecimalPlaces), 2);

    /// <summary>已确认的值（按确定键后更新）。</summary>
    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>最大值限制（可配置）。</summary>
    public decimal MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>最大小数位数（默认 2）。</summary>
    public int MaxDecimalPlaces
    {
        get => GetValue(MaxDecimalPlacesProperty);
        set => SetValue(MaxDecimalPlacesProperty, value);
    }

    /// <summary>当前输入的原始文本（未确认）。</summary>
    public string InputText { get; private set; } = string.Empty;

    /// <summary>确认时触发，参数为确认的值。</summary>
    public event EventHandler<decimal>? Confirmed;

    /// <summary>输入变化时触发，参数为当前文本。</summary>
    public event EventHandler<string>? InputChanged;

    private TextBlock? _display;

    public NumberPad()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _display = this.FindControl<TextBlock>("DisplayText");
        UpdateDisplay();
        // 物理键盘支持
        this.AddHandler(KeyDownEvent, OnKeyDownHandler, RoutingStrategies.Tunnel);
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.Decimal or Key.OemPeriod => ".",
            Key.Back => "backspace",
            Key.Enter => "confirm",
            Key.Escape => "clear",
            _ => null,
        };
        if (key != null)
        {
            PressKey(key);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 按键输入（可测试的核心逻辑）。
    /// key: "0"-"9" / "00" / "." / "backspace" / "clear" / "confirm"
    /// </summary>
    public void PressKey(string key)
    {
        switch (key)
        {
            case "clear":
                InputText = string.Empty;
                break;
            case "backspace":
                if (InputText.Length > 0)
                    InputText = InputText.Substring(0, InputText.Length - 1);
                break;
            case "confirm":
                Confirm();
                return;
            case ".":
                if (!InputText.Contains('.'))
                    InputText = (InputText == string.Empty ? "0" : InputText) + ".";
                break;
            case "00":
                AppendDigits("00");
                break;
            default:
                // 单个数字
                if (key.Length == 1 && char.IsDigit(key[0]))
                    AppendDigits(key);
                break;
        }
        InputChanged?.Invoke(this, InputText);
        UpdateDisplay();
    }

    private void AppendDigits(string digits)
    {
        var candidate = NormalizeLeadingZeros(InputText + digits);
        if (ExceedsDecimalPlaces(candidate))
            return; // 拒绝：小数位超限
        if (TryParse(candidate, out var v) && v > MaxValue)
            return; // 拒绝：超过最大值
        InputText = candidate;
    }

    private static string NormalizeLeadingZeros(string s)
    {
        if (s.StartsWith("0") && s.Length > 1 && s[1] != '.')
        {
            s = s.TrimStart('0');
            if (s == string.Empty || s.StartsWith("."))
                s = "0" + s;
        }
        return s;
    }

    private bool ExceedsDecimalPlaces(string s)
    {
        var dot = s.IndexOf('.');
        if (dot < 0) return false;
        return s.Length - dot - 1 > MaxDecimalPlaces;
    }

    private static bool TryParse(string s, out decimal v)
    {
        if (string.IsNullOrEmpty(s) || s == ".")
        {
            v = 0;
            return s != ".";
        }
        return decimal.TryParse(s, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out v);
    }

    /// <summary>确认当前输入（可测试）。空输入视为 0。</summary>
    public void Confirm()
    {
        if (TryParse(InputText, out var v))
        {
            if (v > MaxValue) v = MaxValue;
            Value = v;
            Confirmed?.Invoke(this, v);
        }
        UpdateDisplay();
    }

    /// <summary>清空输入并重置显示。</summary>
    public void Clear()
    {
        InputText = string.Empty;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_display != null)
            _display.Text = string.IsNullOrEmpty(InputText) ? "0" : InputText;
    }

    // XAML 按钮点击处理（在 axaml 中通过 Click 绑定）
    private void OnDigitClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
            PressKey(tag);
    }
}
