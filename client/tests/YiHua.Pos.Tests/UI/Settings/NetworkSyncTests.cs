using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class NetworkSyncTests
{
    [Fact]
    public void SyncInfo_Offline_ShowsPending()
    {
        var info = new SyncInfoDto
        {
            IsOnline = false,
            PendingCount = 15,
            LastSyncTime = DateTime.Now.AddHours(-2),
            LastError = "网络不可达",
        };
        Assert.False(info.IsOnline);
        Assert.Equal(15, info.PendingCount);
        Assert.NotNull(info.LastSyncTime);
    }

    [Fact]
    public void SyncInfo_Online_NoPending()
    {
        var info = new SyncInfoDto
        {
            IsOnline = true,
            PendingCount = 0,
            LastSyncTime = DateTime.Now,
        };
        Assert.True(info.IsOnline);
        Assert.Equal(0, info.PendingCount);
    }
}
