using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YiHua.Pos.UI.ViewModels.Ordering;

/// <summary>ViewModel 基类（INotifyPropertyChanged）。</summary>
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

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>供外部刷新指定属性（如集合内某行的计算属性）</summary>
    public void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>操作员角色（权限控制用）</summary>
public enum OperatorRole
{
    /// <summary>收银员/服务员</summary>
    Cashier,
    /// <summary>店长</summary>
    Manager,
}
