using YiHua.Pos.UI.Controls.Input;
using Xunit;

namespace YiHua.Pos.Tests.UI;

/// <summary>
/// QtyStepper 加减逻辑测试。
/// </summary>
public class QtyStepperTests
{
    [Fact]
    public void Increment_IncreasesByStep()
    {
        var stepper = new QtyStepper { Value = 1, Step = 1 };
        stepper.Increment();
        Assert.Equal(2m, stepper.Value);
    }

    [Fact]
    public void Decrement_DecreasesByStep()
    {
        var stepper = new QtyStepper { Value = 5, Step = 1 };
        stepper.Decrement();
        Assert.Equal(4m, stepper.Value);
    }

    [Fact]
    public void Value_ClampedToMinimum()
    {
        var stepper = new QtyStepper { Value = 1, Minimum = 1, Step = 1 };
        stepper.Decrement();
        Assert.Equal(1m, stepper.Value); // 不低于最小值
    }

    [Fact]
    public void Value_ClampedToMaximum()
    {
        var stepper = new QtyStepper { Value = 99, Maximum = 99, Step = 1 };
        stepper.Increment();
        Assert.Equal(99m, stepper.Value); // 不超过最大值
    }

    [Fact]
    public void ValueChanged_Raised()
    {
        var stepper = new QtyStepper { Value = 1 };
        decimal? changed = null;
        stepper.ValueChanged += (_, v) => changed = v;
        stepper.Increment();
        Assert.Equal(2m, changed);
    }

    [Fact]
    public void FractionalStep_Supported()
    {
        var stepper = new QtyStepper { Value = 1m, Step = 0.5m, Minimum = 0 };
        stepper.Increment();
        Assert.Equal(1.5m, stepper.Value);
        stepper.Decrement();
        stepper.Decrement();
        Assert.Equal(0.5m, stepper.Value);
    }
}
