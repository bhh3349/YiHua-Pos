using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.GroupBuy;

public partial class GroupBuyRecordView : UserControl
{
    public GroupBuyRecordView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnPlatformChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.GroupBuy.GroupBuyRecordViewModel vm
            && sender is ComboBox cb
            && cb.SelectedItem is ComboBoxItem item
            && item.Content is string text)
        {
            vm.PlatformFilter = text == "全部平台" ? null : text;
        }
    }
}
