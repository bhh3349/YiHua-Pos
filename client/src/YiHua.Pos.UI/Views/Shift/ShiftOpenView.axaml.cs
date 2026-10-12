using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Shift;

namespace YiHua.Pos.UI.Views.Shift;

public partial class ShiftOpenView : UserControl
{
    public ShiftOpenView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private ShiftOpenViewModel? ViewModel => DataContext as ShiftOpenViewModel;

    /// <summary>操作人信息（由调用方设置）</summary>
    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string TerminalNo { get; set; } = string.Empty;

    private async void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.ConfirmAsync(OperatorId, OperatorName, TerminalNo);
    }
}
