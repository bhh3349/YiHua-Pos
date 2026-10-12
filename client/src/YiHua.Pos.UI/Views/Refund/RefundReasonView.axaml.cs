using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Refund;

namespace YiHua.Pos.UI.Views.Refund;

public partial class RefundReasonView : UserControl
{
    public RefundReasonView() => InitializeComponent();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        var list = this.FindControl<ListBox>("ReasonList");
        if (list != null)
        {
            list.ItemsSource = RefundReasonViewModel.ReasonOptions;
            list.SelectionChanged += OnSelectionChanged;
        }
    }

    private RefundReasonViewModel? ViewModel => DataContext as RefundReasonViewModel;

    public event EventHandler? Cancelled;
    public event EventHandler? Confirmed;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || sender is not ListBox lb) return;
        if (lb.SelectedItem is RefundReasonOption opt)
            ViewModel.Reason = opt.Value;
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnConfirm(object? s, RoutedEventArgs e)
    {
        if (ViewModel?.Validate() == true)
            Confirmed?.Invoke(this, EventArgs.Empty);
    }
}
