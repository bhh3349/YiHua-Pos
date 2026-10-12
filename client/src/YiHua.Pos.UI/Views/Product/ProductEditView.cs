using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Product;

/// <summary>
/// 商品编辑页：分段表单（基础/规格/套餐），保存按钮固定底部。
/// </summary>
public partial class ProductEditView : UserControl
{
    public ProductEditView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
