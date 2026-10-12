using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Login;

namespace YiHua.Pos.UI.Views.Login;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private LoginViewModel? ViewModel => DataContext as LoginViewModel;

    private void OnSelectStoreStep(object? sender, RoutedEventArgs e) => ViewModel?.BackToStoreSelect();

    private void OnSelectLoginStep(object? sender, RoutedEventArgs e)
    {
        // 仅当已选门店时允许切换
        if (ViewModel?.StoreSelect.EffectiveStoreId != null)
            _ = ViewModel.NextToLoginAsync();
    }

    private async void OnNext(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
            await ViewModel.NextToLoginAsync();
    }

    private void OnBack(object? sender, RoutedEventArgs e) => ViewModel?.BackToStoreSelect();

    private async void OnLogin(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;
        var storeId = ViewModel.StoreSelect.EffectiveStoreId;
        if (string.IsNullOrEmpty(storeId))
            return;
        await ViewModel.LoginForm.LoginAsync(storeId, default);
    }
}
