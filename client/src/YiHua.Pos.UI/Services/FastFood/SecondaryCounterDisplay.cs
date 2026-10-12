using YiHua.Pos.Hardware.Devices;

namespace YiHua.Pos.UI.Services.FastFood;

/// <summary>
/// 副屏叫号显示（T-106 的 SecondaryScreenManager）。
/// 把叫号数据转成 <see cref="SecondaryScreenData"/> 经由 ShowOrderAsync 推送
///（FromOrder 对 SecondaryScreenData 直接透传）。
/// </summary>
public sealed class SecondaryCounterDisplay : ICounterDisplay
{
    private readonly SecondaryScreenManager _manager;

    public SecondaryCounterDisplay(SecondaryScreenManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public bool IsAvailable => _manager.IsAvailable;

    public void Show(CallBoardData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        string message = data.CurrentStatus;
        if (data.WaitingNumbers.Count > 0)
            message += $"\n等待 {data.WaitingCount} 人：{string.Join(" ", data.WaitingNumbers)}";

        var screenData = new SecondaryScreenData
        {
            State = SecondaryScreenState.Idle,
            Title = data.CurrentNumber,
            Message = message,
            AmountTotal = data.ShowAmount ? data.CurrentAmount : 0m,
        };

        // 同步等待推送完成（调用方在 UI 线程）
        _manager.ShowOrderAsync(screenData).GetAwaiter().GetResult();
    }
}
