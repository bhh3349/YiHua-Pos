using YiHua.Pos.Hardware.Abstractions;

namespace YiHua.Pos.Hardware.Devices;

/// <summary>
/// 无硬件时的客显模拟实现（开发调试用）。
/// 只记录最后一次显示的数据，不做任何 IO。
/// </summary>
public sealed class CustomerDisplaySimulator : ICustomerDisplay
{
    private CustomerDisplayData? _lastShown;

    public bool IsConnected { get; private set; }
    public int ShowCount { get; private set; }
    public int ClearCount { get; private set; }

    /// <summary>最后一次 ShowAsync 传入的数据（Clear 后为 null）</summary>
    public CustomerDisplayData? LastShown => _lastShown;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.FromResult(true);
    }

    public Task ShowAsync(CustomerDisplayData data, CancellationToken ct = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        _lastShown = data;
        ShowCount++;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _lastShown = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
