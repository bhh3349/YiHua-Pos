using Avalonia.Controls;
using YiHua.Pos.UI.Services.Settings;
using YiHua.Pos.UI.ViewModels.Settings;

namespace YiHua.Pos.App.Services;

/// <summary>设置子页面路由及加载器。</summary>
internal sealed class SettingsPageHost : SettingsNavigationViewModel
{
    private readonly ISettingsStore _store;
    private readonly IBackupFileProvider _backup;
    private readonly DiagnosticInfoProvider _diag;

    public SettingsPageHost(ISettingsStore store, IBackupFileProvider backup, DiagnosticInfoProvider diag)
    {
        _store = store;
        _backup = backup;
        _diag = diag;
        Pages = new Dictionary<string, Control>
        {
            ["门店信息"] = new YiHua.Pos.UI.Views.Settings.StoreInfoView { DataContext = new StoreInfoViewModel(store) },
            ["打印机管理"] = new YiHua.Pos.UI.Views.Settings.PrinterSettingsView { DataContext = new PrinterSettingsViewModel(store) },
            ["收银规则"] = new YiHua.Pos.UI.Views.Settings.CashRuleView { DataContext = new CashRuleViewModel(store) },
            ["系统信息与备份"] = new YiHua.Pos.UI.Views.Settings.SystemInfoView { DataContext = new SystemInfoViewModel(backup, diag) },
            ["网络与同步"] = new YiHua.Pos.UI.Views.Settings.NetworkSyncView { DataContext = new NetworkSyncViewModel(store) },
        };
    }

    public IReadOnlyDictionary<string, Control> Pages { get; }

    public override SettingsNavItem? Selected
    {
        get => base.Selected;
        set
        {
            base.Selected = value;
            base.SelectedPage = value == null ? null : Pages.GetValueOrDefault(value.Title);
        }
    }

    public new Control? SelectedPage => base.SelectedPage as Control ?? (Selected == null ? null : Pages.GetValueOrDefault(Selected.Title));

    public async Task LoadSelectedAsync()
    {
        var page = SelectedPage;
        switch (page?.DataContext)
        {
            case StoreInfoViewModel vm: await vm.LoadAsync(); break;
            case PrinterSettingsViewModel vm: await vm.LoadAsync(); break;
            case CashRuleViewModel vm: await vm.LoadAsync(); break;
            case SystemInfoViewModel vm: break;
            case NetworkSyncViewModel vm: await vm.LoadAsync(); break;
        }
        RaiseSelectedChanged();
    }

    private void RaiseSelectedChanged()
    {
        SelectedPageChanged?.Invoke(this, EventArgs.Empty);
    }
    public event EventHandler? SelectedPageChanged;
}