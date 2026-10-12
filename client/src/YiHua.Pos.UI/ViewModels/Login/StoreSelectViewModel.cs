using System.Collections.ObjectModel;
using YiHua.Pos.UI.Services;

namespace YiHua.Pos.UI.ViewModels.Login;

/// <summary>门店选择 ViewModel：服务端拉列表，离线用本地缓存，支持手动输入门店编号</summary>
public class StoreSelectViewModel : ViewModelBase
{
    private readonly ILoginService _loginService;

    private LoginState _state = LoginState.Idle;
    private StoreInfo? _selectedStore;
    private string _manualStoreCode = string.Empty;
    private string _errorMessage = string.Empty;

    public StoreSelectViewModel(ILoginService loginService)
    {
        _loginService = loginService;
        Stores = new ObservableCollection<StoreInfo>();
    }

    public ObservableCollection<StoreInfo> Stores { get; }

    public LoginState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public StoreInfo? SelectedStore
    {
        get => _selectedStore;
        set
        {
            if (SetProperty(ref _selectedStore, value) && value != null)
                ManualStoreCode = string.Empty;
        }
    }

    /// <summary>手动输入的门店编号（网络不通时用）</summary>
    public string ManualStoreCode
    {
        get => _manualStoreCode;
        set => SetProperty(ref _manualStoreCode, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading => State == LoginState.Loading;
    public bool IsEmpty => State == LoginState.Empty;
    public bool HasError => State == LoginState.Error;

    /// <summary>最终选中的门店 ID（列表选择优先，其次手动输入）</summary>
    public string? EffectiveStoreId =>
        SelectedStore?.StoreId is { Length: > 0 } id ? id :
        ManualStoreCode.Trim() is { Length: > 0 } code ? code : null;

    /// <summary>记住本次选择的门店</summary>
    public Task SaveSelectedStoreAsync()
    {
        var id = EffectiveStoreId;
        return id == null ? Task.CompletedTask : _loginService.SaveLastStoreIdAsync(id);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        State = LoginState.Loading;
        ErrorMessage = string.Empty;
        try
        {
            var stores = await _loginService.GetStoresAsync(ct).ConfigureAwait(false);
            Stores.Clear();
            foreach (var s in stores)
                Stores.Add(s);

            // 记住上次选择
            var lastId = _loginService.LastStoreId;
            if (lastId != null)
                SelectedStore = Stores.FirstOrDefault(s => s.StoreId == lastId);

            State = Stores.Count == 0 ? LoginState.Empty : LoginState.Idle;
        }
        catch (Exception ex)
        {
            State = LoginState.Error;
            ErrorMessage = $"门店列表加载失败：{ex.Message}，可手动输入门店编号继续";
        }
    }
}
