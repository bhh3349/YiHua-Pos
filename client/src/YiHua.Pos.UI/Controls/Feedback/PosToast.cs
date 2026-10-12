using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace YiHua.Pos.UI.Controls.Feedback;

/// <summary>
/// 轻提示 Toast：短文本反馈，2 秒自动消失，无动画（直接显示/隐藏）。
/// 用法：var toast = new PosToast { Message = "..." }; host.Children.Add(toast); toast.Show();
/// </summary>
public class PosToast : Border
{
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<PosToast, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<PosToastKind> KindProperty =
        AvaloniaProperty.Register<PosToast, PosToastKind>(nameof(Kind), PosToastKind.Info);

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public PosToastKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private readonly TextBlock _label;
    private CancellationTokenSource? _cts;

    public PosToast()
    {
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16, 12);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 0, 32);
        MaxWidth = 480;
        _label = new TextBlock
        {
            FontSize = 16,
            Foreground = Brushes.White,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        _label.Bind(TextBlock.TextProperty, this.GetObservable(MessageProperty));
        Child = _label;
        IsVisible = false;
        UpdateKind();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
            UpdateKind();
    }

    private void UpdateKind()
    {
        var key = Kind switch
        {
            PosToastKind.Success => "SuccessColor",
            PosToastKind.Warning => "WarningColor",
            PosToastKind.Error => "DangerColor",
            _ => "TextPrimaryColor",
        };
        var brush = new SolidColorBrush(Colors.Black, 0.85);
        brush.Bind(SolidColorBrush.ColorProperty, new DynamicResourceExtension(key));
        Background = brush;
    }

    /// <summary>显示 Toast，duration 后自动隐藏（默认 2 秒）。</summary>
    public async void Show(TimeSpan? duration = null)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsVisible = true;
        try
        {
            await Task.Delay(duration ?? TimeSpan.FromSeconds(2), token);
            if (!token.IsCancellationRequested)
                IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    public void Dismiss()
    {
        _cts?.Cancel();
        IsVisible = false;
    }
}

public enum PosToastKind
{
    Info,
    Success,
    Warning,
    Error,
}
