namespace YiHua.Pos.UI.Services.SoldOut;

/// <summary>时钟抽象（定时逻辑可测，不要写死 DateTime.Now）</summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>系统时钟</summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

/// <summary>测试用固定时钟</summary>
public sealed class FakeClock : IClock
{
    public DateTime Now { get; set; }
    public FakeClock(DateTime now) => Now = now;
}
