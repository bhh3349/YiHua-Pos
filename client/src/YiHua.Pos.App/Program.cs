using Avalonia;

namespace YiHua.Pos.App;

internal static class Program
{
    // Avalonia 初始化前不要使用任何 Avalonia API，也不要初始化 DI 容器
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
