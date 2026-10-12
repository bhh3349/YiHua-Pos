using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.SoldOut;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.SoldOut;

// ViewModels.Product 命名空间已存在（T-302），用别名避免 CS0118
using Product = YiHua.Pos.Domain.Entities.Product;

/// <summary>批量沽清 ViewModel</summary>
public sealed class SoldOutBatchViewModel : ViewModelBase
{
    private readonly SoldOutService _service;
    private string _keyword = string.Empty;
    private string _operatorName = string.Empty;
    private bool _isWorking;
    private string? _errorMessage;
    private string? _confirmText;

    public SoldOutBatchViewModel(SoldOutService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>全部商品（调用方传入，用于筛选）</summary>
    public List<Product> AllProducts { get; set; } = new();

    /// <summary>当前选中的商品ID</summary>
    public HashSet<long> SelectedIds { get; } = new();

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value ?? string.Empty);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value ?? string.Empty);
    }

    /// <summary>确认范围文本（"将沽清热菜档 8 道菜"）</summary>
    public string? ConfirmText
    {
        get => _confirmText;
        private set => SetProperty(ref _confirmText, value);
    }

    public bool IsWorking
    {
        get => _isWorking;
        private set => SetProperty(ref _isWorking, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>按档口选中</summary>
    public void SelectByCategory(long categoryId, string categoryName)
    {
        SelectedIds.Clear();
        foreach (var id in SoldOutCalculator.FilterByCategory(AllProducts, categoryId))
            SelectedIds.Add(id);
        ConfirmText = $"将沽清{categoryName} {SelectedIds.Count} 道菜";
        RaisePropertyChanged(nameof(SelectedIds));
    }

    /// <summary>按关键字选中（无匹配返回空，不报错）</summary>
    public void SelectByKeyword()
    {
        SelectedIds.Clear();
        foreach (var id in SoldOutCalculator.FilterByKeyword(AllProducts, Keyword))
            SelectedIds.Add(id);
        ConfirmText = SelectedIds.Count == 0
            ? $"未找到包含「{Keyword}」的商品"
            : $"将沽清包含「{Keyword}」的 {SelectedIds.Count} 道菜";
        RaisePropertyChanged(nameof(SelectedIds));
    }

    public void ClearSelection()
    {
        SelectedIds.Clear();
        ConfirmText = null;
        RaisePropertyChanged(nameof(SelectedIds));
    }

    /// <summary>执行批量沽清</summary>
    public async Task<int> ConfirmBatchAsync(CancellationToken ct = default)
    {
        if (SelectedIds.Count == 0)
        {
            ErrorMessage = "请先选择要沽清的商品";
            return 0;
        }

        IsWorking = true;
        ErrorMessage = null;
        try
        {
            var products = AllProducts.Where(p => SelectedIds.Contains(p.Id)).ToList();
            int count = await _service.BatchSellOutAsync(products, OperatorName, ct)
                                      .ConfigureAwait(false);
            ClearSelection();
            return count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"批量沽清失败：{ex.Message}";
            return 0;
        }
        finally
        {
            IsWorking = false;
        }
    }
}
