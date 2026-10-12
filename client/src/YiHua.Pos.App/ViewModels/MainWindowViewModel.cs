using System.Runtime.InteropServices;
using Prism.Mvvm;

namespace YiHua.Pos.App.ViewModels;

public class MainWindowViewModel : BindableBase
{
    public string Title => "逸华餐饮收银";

    public string BuildInfo =>
        $"v1.0.0 · {RuntimeInformation.FrameworkDescription} · {RuntimeInformation.ProcessArchitecture}";

    public string RuntimeInfo =>
        $"运行时: {RuntimeInformation.FrameworkDescription}\n" +
        $"架构: {RuntimeInformation.ProcessArchitecture}\n" +
        $"系统: {RuntimeInformation.OSDescription}";
}
