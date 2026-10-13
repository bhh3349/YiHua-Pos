using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.Views.Ordering;

/// <summary>退菜原因弹窗：预设原因点选，或自己填。</summary>
public partial class RefundReasonDialog : Window
{
    public RefundReasonDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private RefundReasonViewModel? Vm => DataContext as RefundReasonViewModel;

    /// <summary>确认退菜，带回原因文本。</summary>
    public event EventHandler<string>? Confirmed;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        if (!vm.HasReason)
        {
            // 退菜必须写原因：对账时「为什么少了这道菜」全靠它
            this.FindControl<TextBlock>("ErrorText")!.Text = "请选择或填写退菜原因";
            return;
        }

        Confirmed?.Invoke(this, vm.Reason!);
        Close();
    }
}