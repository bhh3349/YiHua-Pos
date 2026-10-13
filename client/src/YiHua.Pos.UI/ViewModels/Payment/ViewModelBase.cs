using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 结账模块的 ViewModel 基类。
/// 各模块各自持有一份（UI 项目内不做跨命名空间共享），只提供属性通知。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        RaisePropertyChanged(name);
        return true;
    }
}