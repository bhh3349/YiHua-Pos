using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.OrderManage;

namespace YiHua.Pos.UI.Views.OrderManage;

public partial class ReverseSettleDialog : UserControl
{
    public ReverseSettleDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ReverseSettleViewModel? ViewModel => DataContext as ReverseSettleViewModel;
    public event EventHandler? Cancelled;

    /// <summary>操作人角色（由调用方设置，用于权限检查）</summary>
    public string OperatorRole { get; set; } = string.Empty;

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.ConfirmAsync(OperatorRole);
    }
}
