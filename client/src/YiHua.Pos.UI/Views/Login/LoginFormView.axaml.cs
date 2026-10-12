using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YiHua.Pos.UI.ViewModels.Login;

namespace YiHua.Pos.UI.Views.Login;

public partial class LoginFormView : UserControl
{
    public LoginFormView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnTogglePassword(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginFormViewModel vm)
            return;
        vm.IsPasswordVisible = !vm.IsPasswordVisible;
        if (this.FindControl<Button>("TogglePasswordButton") is { } btn)
            btn.Content = vm.IsPasswordVisible ? "隐藏" : "显示";
        if (this.FindControl<TextBox>("PasswordBox") is { } box)
            box.PasswordChar = vm.IsPasswordVisible ? '\0' : '•';
    }
}
