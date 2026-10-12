using System.Collections.ObjectModel;
using YiHua.Pos.Domain.Entities;
using YiHua.Pos.UI.Services.SoldOut;
using YiHua.Pos.UI.ViewModels.Ordering;

namespace YiHua.Pos.UI.ViewModels.SoldOut;

/// <summary>沽清列表 ViewModel（按档口分组）</summary>
public sealed class SoldOutListViewModel : ViewModelBase
{
    private readonly SoldOutService _service;
    private readonly ISoldOutRepository _repo;
    private bool _isLoading;
    private string? _errorMessage;

    public SoldOutListViewModel(SoldOutService service, ISoldOutRepository repo)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    /// <summary>按档口分组的沽清记录</summary>
    public ObservableCollection<SoldOutCategoryGroup> Groups { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsEmpty => Groups.Count == 0;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var all = await _repo.GetAllAsync(ct).ConfigureAwait(false);
            Groups.Clear();
            foreach (var g in all.GroupBy(r => new { r.CategoryId, r.CategoryName })
                                 .OrderBy(g => g.Key.CategoryName))
            {
                var group = new SoldOutCategoryGroup
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = string.IsNullOrEmpty(g.Key.CategoryName)
                        ? "未分类" : g.Key.CategoryName,
                };
                foreach (var r in g.OrderBy(r => r.ProductName))
                    group.Records.Add(r);
                Groups.Add(group);
            }
            RaisePropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载沽清列表失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RestoreAsync(long productId, CancellationToken ct = default)
    {
        await _service.RestoreAsync(productId, ct).ConfigureAwait(false);
        await LoadAsync(ct).ConfigureAwait(false);
    }

    public async Task RestoreAllAsync(CancellationToken ct = default)
    {
        await _service.RestoreAllAsync(ct).ConfigureAwait(false);
        await LoadAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>档口分组</summary>
public sealed class SoldOutCategoryGroup
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public ObservableCollection<SoldOutRecord> Records { get; } = new();
    public int Count => Records.Count;
}
