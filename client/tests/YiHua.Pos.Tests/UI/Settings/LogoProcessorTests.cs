using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

public class LogoProcessorTests
{
    [Fact]
    public void Validate_Empty_Rejected()
    {
        Assert.False(LogoProcessor.Validate(null, "logo.png", out var error));
        Assert.Contains("选择", error);
        Assert.False(LogoProcessor.Validate(Array.Empty<byte>(), "logo.png", out _));
    }

    [Fact]
    public void Validate_TooLarge_Rejected()
    {
        var big = new byte[300 * 1024]; // 300KB
        Assert.False(LogoProcessor.Validate(big, "logo.png", out var error));
        Assert.Contains("太大", error);
        Assert.Contains("200KB", error);
    }

    [Fact]
    public void Validate_UnsupportedFormat_Rejected()
    {
        var data = new byte[1024];
        Assert.False(LogoProcessor.Validate(data, "logo.gif", out var error));
        Assert.Contains("不支持", error);
    }

    [Fact]
    public void Validate_Valid_Passes()
    {
        var data = new byte[50 * 1024]; // 50KB
        Assert.True(LogoProcessor.Validate(data, "logo.png", out _));
        Assert.True(LogoProcessor.Validate(data, "logo.JPG", out _)); // 大小写不敏感
    }

    [Fact]
    public void GetTargetSize_ScalesDown()
    {
        var (w, h) = LogoProcessor.GetTargetSize(800, 400);
        Assert.True(w <= 400);
        Assert.True(h <= 200);
        Assert.Equal(2.0, (double)w / h, 1); // 保持比例
    }

    [Fact]
    public void GetTargetSize_SmallKeepsOriginal()
    {
        var (w, h) = LogoProcessor.GetTargetSize(200, 100);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }
}
