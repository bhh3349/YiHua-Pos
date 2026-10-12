using Xunit;
using YiHua.Pos.Hardware.Devices;

namespace YiHua.Pos.Tests.Hardware.Devices;

public class VfdCommandBuilderTests
{
    [Fact]
    public void Clear_Emits_FormFeed()
    {
        byte[] bytes = new VfdCommandBuilder().Clear().Build();
        Assert.Equal(new byte[] { 0x0C }, bytes);
    }

    [Fact]
    public void Init_Emits_EscAt()
    {
        byte[] bytes = new VfdCommandBuilder().Init().Build();
        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes);
    }

    [Fact]
    public void SetCursor_Emits_EscL_Row_Col()
    {
        byte[] bytes = new VfdCommandBuilder().SetCursor(1, 5).Build();
        Assert.Equal(new byte[] { 0x1B, 0x6C, 0x01, 0x05 }, bytes);
    }

    [Fact]
    public void Brightness_Emits_EscStar_Level()
    {
        byte[] bytes = new VfdCommandBuilder().Brightness(3).Build();
        Assert.Equal(new byte[] { 0x1B, 0x2A, 0x03 }, bytes);
    }

    [Fact]
    public void Text_Emits_AsciiBytes()
    {
        byte[] bytes = new VfdCommandBuilder().Text("Total:128.00").Build();
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("Total:128.00"), bytes);
    }

    [Fact]
    public void Sanitize_Replaces_NonAscii_With_QuestionMark()
    {
        Assert.Equal("??", VfdCommandBuilder.Sanitize("合计"));
        Assert.Equal("abc", VfdCommandBuilder.Sanitize("abc"));
        Assert.Equal("a??b", VfdCommandBuilder.Sanitize("a合计b"));
    }

    [Fact]
    public void Sanitize_Strips_ControlChars()
    {
        Assert.Equal("ab", VfdCommandBuilder.Sanitize("a\nb"));
        Assert.Equal("ab", VfdCommandBuilder.Sanitize("a\rb"));
        Assert.Equal("ab", VfdCommandBuilder.Sanitize("a\tb"));
        Assert.Equal("ab", VfdCommandBuilder.Sanitize("a\u0001b\u007F"));
    }

    [Fact]
    public void TruncateByColumns_Cuts_To_Columns()
    {
        Assert.Equal("12345", VfdCommandBuilder.TruncateByColumns("1234567890", 5));
        Assert.Equal("abc", VfdCommandBuilder.TruncateByColumns("abc", 20));
    }

    [Fact]
    public void FullSequence_Clear_Cursor_Text()
    {
        byte[] bytes = new VfdCommandBuilder()
            .Clear()
            .SetCursor(0, 0)
            .Text("Hello")
            .SetCursor(1, 0)
            .Text("World")
            .Build();

        var expected = new List<byte> { 0x0C, 0x1B, 0x6C, 0x00, 0x00 };
        expected.AddRange(System.Text.Encoding.ASCII.GetBytes("Hello"));
        expected.AddRange(new byte[] { 0x1B, 0x6C, 0x01, 0x00 });
        expected.AddRange(System.Text.Encoding.ASCII.GetBytes("World"));
        Assert.Equal(expected.ToArray(), bytes);
    }

    [Fact]
    public void SetCursor_Throws_On_OutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VfdCommandBuilder().SetCursor(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VfdCommandBuilder().SetCursor(0, 300));
    }
}
