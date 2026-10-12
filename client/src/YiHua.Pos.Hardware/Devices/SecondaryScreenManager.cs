using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Devices;

/// <summary>副屏状态</summary>
public enum SecondaryScreenState
{
    /// <summary>空闲：欢迎语 / 广告</summary>
    Idle,

    /// <summary>点单中：商品 + 金额</summary>
    Ordering,

    /// <summary>结账：实收 + 找零</summary>
    Settling,
}

/// <summary>副屏上的商品行</summary>
public sealed class SecondaryScreenItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>副屏数据模型（UI 层按此渲染）</summary>
public sealed class SecondaryScreenData
{
    public SecondaryScreenState State { get; set; } = SecondaryScreenState.Idle;
    public string Title { get; set; } = "欢迎光临";
    public string Message { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public List<SecondaryScreenItem> Items { get; set; } = new();
    public decimal AmountTotal { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal AmountChange { get; set; }

    /// <summary>
    /// 从业务订单对象适配。支持：
    /// 1) 直接传 SecondaryScreenData；
    /// 2) 传 IEnumerable&lt;SecondaryScreenItem&gt;；
    /// 3) 其他对象：按约定反射读取 Items / AmountTotal / AmountReceived / AmountChange / Title 属性（尽力而为）。
    /// </summary>
    public static SecondaryScreenData FromOrder(object? order)
    {
        var data = new SecondaryScreenData { State = SecondaryScreenState.Ordering };
        if (order == null)
            return data;
        if (order is SecondaryScreenData d)
            return d;
        if (order is IEnumerable<SecondaryScreenItem> items)
        {
            data.Items.AddRange(items);
            data.AmountTotal = data.Items.Sum(i => i.Amount);
            return data;
        }

        var t = order.GetType();
        data.Title = ReadString(t, order, "Title", "DeskName") ?? data.Title;
        data.AmountTotal = ReadDecimal(t, order, "AmountTotal", "Total", "TotalAmount");
        data.AmountReceived = ReadDecimal(t, order, "AmountReceived", "Received");
        data.AmountChange = ReadDecimal(t, order, "AmountChange", "Change");
        return data;
    }

    private static string? ReadString(Type t, object o, params string[] names)
    {
        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(string))
                return (string?)p.GetValue(o);
        }
        return null;
    }

    private static decimal ReadDecimal(Type t, object o, params string[] names)
    {
        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p == null) continue;
            try
            {
                var v = p.GetValue(o);
                if (v != null) return Convert.ToDecimal(v);
            }
            catch { /* ignore, try next */ }
        }
        return 0m;
    }
}

/// <summary>
/// 副屏视图接口：由 UI 层用 Avalonia 实现（全屏窗口）。
/// 管理器只负责数据与状态，实际渲染交给 UI 层。
/// </summary>
public interface ISecondaryScreenView
{
    void Show(SecondaryScreenData data);
    void Hide();
}

/// <summary>
/// 副屏管理器：检测第二块屏、推送数据。
/// Avalonia 的具体窗口由 UI 层实现 <see cref="ISecondaryScreenView"/> 后注入；
/// 屏幕检测默认返回单屏，UI 层可注入 Avalonia 的 Screens.ScreenCount。
/// </summary>
public sealed class SecondaryScreenManager : ISecondaryScreen
{
    private readonly Func<int> _screenCountProvider;
    private readonly ISecondaryScreenView? _view;
    private SecondaryScreenData _current = new();

    /// <summary>数据变化事件（UI 层也可订阅此事件自行渲染）</summary>
    public event EventHandler<SecondaryScreenData>? DataChanged;

    public SecondaryScreenManager(
        Func<int>? screenCountProvider = null,
        ISecondaryScreenView? view = null)
    {
        _screenCountProvider = screenCountProvider ?? (() => 1);
        _view = view;
    }

    /// <summary>第二块屏存在时为 true</summary>
    public bool IsAvailable => _screenCountProvider() > 1;

    /// <summary>当前副屏数据（测试 / 调试用）</summary>
    public SecondaryScreenData Current => _current;

    public Task ShowOrderAsync(object order, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return PublishAsync(SecondaryScreenData.FromOrder(order), ct);
    }

    public Task ShowIdleAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return PublishAsync(new SecondaryScreenData
        {
            State = SecondaryScreenState.Idle,
            Title = "欢迎光临",
            Message = "请触屏点餐",
        }, ct);
    }

    public Task ShowAdAsync(string imagePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return PublishAsync(new SecondaryScreenData
        {
            State = SecondaryScreenState.Idle,
            Title = string.Empty,
            ImagePath = imagePath,
        }, ct);
    }

    /// <summary>结账态：实收 + 找零（扩展方法，接口之外）</summary>
    public Task ShowSettlingAsync(decimal total, decimal received, decimal change, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return PublishAsync(new SecondaryScreenData
        {
            State = SecondaryScreenState.Settling,
            Title = "谢谢惠顾",
            AmountTotal = total,
            AmountReceived = received,
            AmountChange = change,
        }, ct);
    }

    private Task PublishAsync(SecondaryScreenData data, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _current = data;
        DataChanged?.Invoke(this, data);
        if (IsAvailable)
            _view?.Show(data);
        else
            _view?.Hide();
        return Task.CompletedTask;
    }
}
