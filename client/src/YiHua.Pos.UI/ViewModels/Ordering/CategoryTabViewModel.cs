using YiHua.Pos.Domain.Entities;

namespace YiHua.Pos.UI.ViewModels.Ordering;

/// <summary>分类标签 ViewModel（带选中态）</summary>
public sealed class CategoryTabViewModel : ViewModelBase
{
    private bool _isSelected;

    public CategoryTabViewModel(ProductCategory category)
    {
        Category = category;
    }

    public ProductCategory Category { get; }
    public string Name => Category.Name;
    public long Id => Category.Id;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
