using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.GroupBuy;

namespace YiHua.Pos.UI.Views.GroupBuy;

public partial class CouponVerifyView : UserControl
{
    public CouponVerifyView() => InitializeComponent();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        var box = this.FindControl<TextBox>("ScanBox");
        if (box != null)
        {
            // autofocus：页面打开即聚焦，扫码枪直接扫
            box.AttachedToVisualTree += (_, _) => box.Focus();
            // 扫码枪回车即触发验券
            box.KeyDown += OnScanBoxKeyDown;
        }
    }

    private CouponVerifyViewModel? ViewModel => DataContext as CouponVerifyViewModel;

    /// <summary>扫码枪配置：前缀/后缀过滤（部分扫码枪带 F9 触发）</summary>
    public string? ScannerPrefix { get; set; }
    public string? ScannerSuffix { get; set; }

    public event EventHandler? Redeemed;

    private async void OnScanBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel != null)
        {
            e.Handled = true;
            await ViewModel.VerifyAsync(ScannerPrefix, ScannerSuffix);
        }
    }

    private void OnReset(object? s, RoutedEventArgs e)
    {
        ViewModel?.Reset();
        this.FindControl<TextBox>("ScanBox")?.Focus();
    }

    private async void OnRedeem(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null && await ViewModel.RedeemAsync())
            Redeemed?.Invoke(this, EventArgs.Empty);
    }
}
