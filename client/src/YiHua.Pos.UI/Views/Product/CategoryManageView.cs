using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.UI.Views.Product;

/// <summary>
/// 分类管理页：新建 / 重命名 / 隐藏 / 删除。
/// 每次操作都写库并重新载入，避免界面显示的和库里不一致。
/// </summary>
public partial class CategoryManageView : UserControl
{
    public CategoryManageView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private CategoryManageViewModel? Vm => DataContext as CategoryManageViewModel;

    /// <summary>返回商品列表。</summary>
    public event EventHandler? CloseRequested;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var vm = Vm;
        if (vm == null) return;
        await vm.LoadAsync();
        this.FindControl<ItemsControl>("CategoryList")!.ItemsSource = vm.Categories;
    }

    private async void OnAdd(object? sender, RoutedEventArgs e)    {
        var vm = Vm;
        if (vm == null) return;

        var box = this.FindControl<TextBox>("NewCategoryBox")!;
        var (ok, error) = await vm.AddAsync(box.Text ?? string.Empty);
        if (ok)
        {
            box.Text = string.Empty;
            ShowMessages(null, vm.InfoMessage);
            await RefreshAsync();
        }
        else
        {
            ShowMessages(error, null);
        }
    }

    private async void OnRename(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null || sender is not Control c || c.DataContext is not CategoryDto dto) return;

        // 行内的名字输入框就是新名字：商家改完直接点重命名
        var newName = FindRowName(c) ?? dto.Name;
        var (ok, error) = await vm.RenameAsync(dto.Id, newName);
        ShowMessages(ok ? null : error, ok ? $"已重命名为「{newName.Trim()}」" : null);
        if (ok) await RefreshAsync();
    }

    private async void OnHide(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null || sender is not Control c || c.DataContext is not CategoryDto dto) return;

        var hide = dto.DataStatus != 99;

        // 先把名字保存下来，再改显示状态，避免改了名字没保存就切走
        var (ok, error) = await vm.RenameAsync(dto.Id, FindRowName(c) ?? dto.Name);
        if (!ok) { ShowMessages(error, null); return; }

        if (hide) vm.HideCategory(dto.Id); else vm.ShowCategory(dto.Id);
        await vm.LoadAsync();
        await RefreshAsync();
        ShowMessages(null, hide ? $"已隐藏「{dto.Name}」" : $"已显示「{dto.Name}」");
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null || sender is not Control c || c.DataContext is not CategoryDto dto) return;

        var (ok, error) = await vm.DeleteAsync(dto.Id);
        ShowMessages(ok ? null : error, ok ? vm.InfoMessage : null);
        if (ok) await RefreshAsync();
    }

    /// <summary>取该行输入框里的当前文本。</summary>
    private static string? FindRowName(Control rowButton)
    {
        if (rowButton.Parent is not Grid grid) return null;
        foreach (var child in grid.Children)
        {
            if (child is TextBox tb && Grid.GetColumn(tb) == 0)
                return tb.Text;
        }
        return null;
    }

    private void ShowMessages(string? error, string? info)
    {
        this.FindControl<TextBlock>("ErrorText")!.Text = error ?? string.Empty;
        this.FindControl<TextBlock>("InfoText")!.Text = info ?? string.Empty;
    }

    private void OnBack(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}