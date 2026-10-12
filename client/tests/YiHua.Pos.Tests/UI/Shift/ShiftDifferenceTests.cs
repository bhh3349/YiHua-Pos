using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.UI.Services.Shift;
using Xunit;

namespace YiHua.Pos.Tests.UI.Shift;

// 别名在 namespace 内部：YiHua.Pos.Tests.UI.Payment 子命名空间会撞名（CS0118）
using PaymentEntity = YiHua.Pos.Domain.Entities.Payment;

/// <summary>差异测试：短款/溢余/零差额的分类与显示</summary>
public class ShiftDifferenceTests
{
    [Fact]
    public void Classify_Shortage()
    {
        Assert.Equal(DifferenceKind.Shortage, ShiftDifferencePolicy.Classify(-70m));
    }

    [Fact]
    public void Classify_Overage()
    {
        Assert.Equal(DifferenceKind.Overage, ShiftDifferencePolicy.Classify(250m));
    }

    [Fact]
    public void Classify_Balanced()
    {
        Assert.Equal(DifferenceKind.Balanced, ShiftDifferencePolicy.Classify(0m));
    }

    [Fact]
    public void Format_Shows_Sign_And_Absolute()
    {
        Assert.Equal("短款 ¥70.00", ShiftDifferencePolicy.FormatDifference(-70m));
        Assert.Equal("溢余 ¥250.00", ShiftDifferencePolicy.FormatDifference(250m));
        Assert.Equal("持平", ShiftDifferencePolicy.FormatDifference(0m));
    }

    [Fact]
    public void RequiresManager_When_Shortage_Over_Threshold()
    {
        var policy = new ShiftDifferencePolicy { ShortageThreshold = 100m };

        Assert.True(policy.RequiresManager(-150m));  // 超阈值需店长
        Assert.False(policy.RequiresManager(-50m));  // 未超阈值不需
        Assert.False(policy.RequiresManager(200m));  // 溢余不需
    }
}
