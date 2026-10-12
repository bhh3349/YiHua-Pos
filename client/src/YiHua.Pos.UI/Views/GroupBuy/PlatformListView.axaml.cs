using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.ObjectModel;

namespace YiHua.Pos.UI.Views.GroupBuy;

/// <summary>平台项（只读展示）</summary>
public class PlatformItem
{
    public string Name { get; }
    public bool Enabled { get; }
    public string StatusText { get; }
    public string BadgeText => Enabled ? "● 已启用" : "○ 未接入";
    public PlatformItem(string name, bool enabled, string status)
    { Name = name; Enabled = enabled; StatusText = status; }
}

public partial class PlatformListView : UserControl
{
    public PlatformListView() => InitializeComponent();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        var list = this.FindControl<ListBox>("PlatformList");
        if (list != null)
        {
            list.ItemsSource = new ObservableCollection<PlatformItem>
            {
                new("美团", true, "已启用"),
                new("饿了么", true, "已启用"),
                new("抖音", false, "未接入"),
            };
        }
    }
}
