using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Product;

namespace YiHua.Pos.UI.Views.Product;

/// <summary>
/// 商品编辑页：把表单控件与 ProductEditModel 双向对接。
///
/// 为什么用手工对接而不是直接 XAML 绑定：
/// 分类/单位下拉要运行时填充，而 Model 是普通 POCO 不带变更通知。
/// 手工对接更直白，出问题也更好定位。
/// </summary>
public partial class ProductEditView : UserControl
{
    public ProductEditView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private ProductEditViewModel? Vm => DataContext as ProductEditViewModel;

    /// <summary>保存成功（由 App 层刷新列表）。</summary>
    public event EventHandler<ProductEditModel>? Saved;

    /// <summary>取消/返回。</summary>
    public event EventHandler? Cancelled;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FillForm();
    }

    /// <summary>把 Model 填进控件（载入或切换商品后调用）。</summary>
    public void FillForm()
    {
        var vm = Vm;
        if (vm == null) return;
        var m = vm.Model;

        this.FindControl<TextBox>("NameBox")!.Text = m.Name;
        this.FindControl<TextBox>("AliasBox")!.Text = string.Join(",", m.Aliases);
        this.FindControl<TextBox>("PriceBox")!.Text = m.Price.ToString("0.00");
        this.FindControl<CheckBox>("WeightedCheck")!.IsChecked = m.IsWeighted;
        this.FindControl<CheckBox>("MarketPriceCheck")!.IsChecked = m.IsMarketPrice;

        var catBox = this.FindControl<ComboBox>("CategoryBox")!;
        catBox.ItemsSource = vm.Categories;
        catBox.SelectedItem = vm.Categories.FirstOrDefault(c => c.Id == m.CategoryId);

        var unitBox = this.FindControl<ComboBox>("UnitBox")!;
        unitBox.ItemsSource = vm.Units;
        unitBox.SelectedItem = vm.Units.FirstOrDefault(u => u.Id == m.UnitId);

        // 没有分类/单位可选时给明确指引，不要让商家对着空下拉框干瞪眼
        if (vm.Categories.Count == 0 || vm.Units.Count == 0)
            SetError("还没有商品分类或计量单位。请先到「商品 → 分类管理」建好，再回来加菜。");
    }

    /// <summary>把控件读回 Model（保存前调用）。</summary>
    public void CollectForm()
    {
        var vm = Vm;
        if (vm == null) return;
        var m = vm.Model;

        m.Name = this.FindControl<TextBox>("NameBox")!.Text?.Trim() ?? string.Empty;
        m.Aliases = (this.FindControl<TextBox>("AliasBox")!.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var priceText = this.FindControl<TextBox>("PriceBox")!.Text?.Trim() ?? "0";
        m.Price = decimal.TryParse(priceText, out var price) ? price : 0m;

        m.IsWeighted = this.FindControl<CheckBox>("WeightedCheck")!.IsChecked == true;
        m.IsMarketPrice = this.FindControl<CheckBox>("MarketPriceCheck")!.IsChecked == true;

        if (this.FindControl<ComboBox>("CategoryBox")!.SelectedItem is CategoryDto cat)
        {
            m.CategoryId = cat.Id;
            m.CategoryName = cat.Name;
        }
        if (this.FindControl<ComboBox>("UnitBox")!.SelectedItem is UnitDto unit)
        {
            m.UnitId = unit.Id;
            m.UnitName = unit.Name;
        }
    }

    private void SetError(string? message)
        => this.FindControl<TextBlock>("ErrorText")!.Text = message ?? string.Empty;

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm == null) return;

        CollectForm();
        var (ok, errors) = await vm.SaveAsync();
        if (ok)
        {
            SetError(null);
            Saved?.Invoke(this, vm.Model);
        }
        else
        {
            // 校验失败要把「哪里不对」写在按钮旁边，不能只让按钮没反应
            SetError(string.Join("；", errors));
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnSectionBasic(object? sender, RoutedEventArgs e) => SwitchSection(EditSection.Basic);
    private void OnSectionSpecs(object? sender, RoutedEventArgs e) => SwitchSection(EditSection.Specs);
    private void OnSectionBundle(object? sender, RoutedEventArgs e) => SwitchSection(EditSection.Bundle);

    private void SwitchSection(EditSection section)
    {
        if (Vm == null) return;
        Vm.CurrentSection = section;
        var skuHost = this.FindControl<ContentControl>("SkuEditorHost");
        var bundleHost = this.FindControl<ContentControl>("BundleEditorHost");
        if (skuHost != null) skuHost.IsVisible = section == EditSection.Specs;
        if (bundleHost != null) bundleHost.IsVisible = section == EditSection.Bundle;
    }
}