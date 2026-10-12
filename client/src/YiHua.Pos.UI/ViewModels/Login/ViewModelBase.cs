using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YiHua.Pos.UI.ViewModels.Login;

/// <summary>ViewModel 基类（UI 项目未引用 Prism，手动实现 INotifyPropertyChanged）</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>登录状态机：idle / loading / empty / error / success / offline</summary>
public enum LoginState
{
    Idle,
    Loading,
    Empty,
    Error,
    Success,
    Offline,
}

/// <summary>登录步骤：门店选择 → 账号密码</summary>
public enum LoginStep
{
    StoreSelect,
    LoginForm,
}
