using Avalonia;

namespace YiHua.Pos.App;

internal static class Program
{
    // Avalonia 初始化前不要使用任何 Avalonia API，也不要初始化 DI 容器
    //
    // ⚠️ Main 必须保持「表达式体」形式：AppBuilder.Configure<App>() 依赖
    // 这个形态做入口识别。改成语句体（哪怕只是多包一层 try/catch）会导致
    // 运行时抛 XamlLoadException: No precompiled XAML found for YiHua.Pos.App.App，
    // 而且编译期完全正常 —— 这个坑踩过一次。
    //
    // 全局异常捕获因此放在 App.Initialize() 里装，而不是 Main。
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia 设计器需要这个方法
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}