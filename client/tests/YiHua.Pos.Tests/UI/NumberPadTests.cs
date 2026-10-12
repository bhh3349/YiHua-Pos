using YiHua.Pos.UI.Controls.Input;
using Xunit;

namespace YiHua.Pos.Tests.UI;

/// <summary>
/// NumberPad 输入逻辑测试（无头，不依赖渲染）。
/// </summary>
public class NumberPadTests
{
    [Fact]
    public void PressDigit_AppendsToInput()
    {
        var pad = new NumberPad();
        pad.PressKey("1");
        pad.PressKey("2");
        pad.PressKey("8");
        Assert.Equal("128", pad.InputText);
    }

    [Fact]
    public void PressDecimalPoint_OnlyOneAllowed()
    {
        var pad = new NumberPad();
        pad.PressKey("1");
        pad.PressKey(".");
        pad.PressKey("2");
        pad.PressKey("."); // 第二个小数点被拒绝
        pad.PressKey("5");
        Assert.Equal("1.25", pad.InputText);
    }

    [Fact]
    public void PressDecimalPoint_First_ProducesZeroDot()
    {
        var pad = new NumberPad();
        pad.PressKey(".");
        Assert.Equal("0.", pad.InputText);
    }

    [Fact]
    public void PressDoubleZero_AppendsTwoZeros()
    {
        var pad = new NumberPad();
        pad.PressKey("1");
        pad.PressKey("00");
        Assert.Equal("100", pad.InputText);
    }

    [Fact]
    public void Backspace_RemovesLastChar()
    {
        var pad = new NumberPad();
        pad.PressKey("1");
        pad.PressKey("2");
        pad.PressKey("backspace");
        Assert.Equal("1", pad.InputText);
        pad.PressKey("backspace");
        Assert.Equal("", pad.InputText);
        // 空输入退格不抛异常
        pad.PressKey("backspace");
        Assert.Equal("", pad.InputText);
    }

    [Fact]
    public void Clear_ResetsInput()
    {
        var pad = new NumberPad();
        pad.PressKey("1");
        pad.PressKey("2");
        pad.PressKey("clear");
        Assert.Equal("", pad.InputText);
    }

    [Fact]
    public void LeadingZeros_AreNormalized()
    {
        var pad = new NumberPad();
        pad.PressKey("0");
        pad.PressKey("0");
        pad.PressKey("5");
        Assert.Equal("5", pad.InputText);
    }

    [Fact]
    public void MaxDecimalPlaces_Enforced()
    {
        var pad = new NumberPad { MaxDecimalPlaces = 2 };
        pad.PressKey("1");
        pad.PressKey(".");
        pad.PressKey("2");
        pad.PressKey("5");
        pad.PressKey("9"); // 第三位小数被拒绝
        Assert.Equal("1.25", pad.InputText);
    }

    [Fact]
    public void MaxValue_Enforced()
    {
        var pad = new NumberPad { MaxValue = 100m };
        pad.PressKey("9");
        pad.PressKey("9");
        pad.PressKey("9"); // 超 100 被拒绝
        Assert.Equal("99", pad.InputText);
    }

    [Fact]
    public void Confirm_SetsValue_AndRaisesEvent()
    {
        var pad = new NumberPad();
        decimal? confirmed = null;
        pad.Confirmed += (_, v) => confirmed = v;
        pad.PressKey("1");
        pad.PressKey("2");
        pad.PressKey(".");
        pad.PressKey("5");
        pad.PressKey("confirm");
        Assert.Equal(12.5m, pad.Value);
        Assert.Equal(12.5m, confirmed);
    }

    [Fact]
    public void Confirm_EmptyInput_TreatedAsZero()
    {
        var pad = new NumberPad();
        pad.PressKey("confirm");
        Assert.Equal(0m, pad.Value);
    }

    [Fact]
    public void InputChanged_RaisedOnEachKey()
    {
        var pad = new NumberPad();
        var changes = new List<string>();
        pad.InputChanged += (_, t) => changes.Add(t);
        pad.PressKey("1");
        pad.PressKey("2");
        Assert.Equal(new[] { "1", "12" }, changes);
    }
}
