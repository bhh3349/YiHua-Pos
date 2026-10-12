using Avalonia.Controls;
using Avalonia.Interactivity;
using YiHua.Pos.UI.Services.SoldOut;

namespace YiHua.Pos.UI.Views.SoldOut;

/// <summary>沽清打印（出餐口张贴）</summary>
public partial class SoldOutPrintView : UserControl
{
    public SoldOutPrintView()
    {
        InitializeComponent();
    }

    public void SetItems(IEnumerable<SoldOutRecord> records)
    {
        var items = this.FindControl<ItemsControl>("PrintItems");
        var time = this.FindControl<TextBlock>("PrintTime");
        if (items != null) items.ItemsSource = records.ToList();
        if (time != null) time.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }

    private void OnPrint(object? sender, RoutedEventArgs e)
        => PrintRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler? PrintRequested;
}
