using Xunit;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Devices;

namespace YiHua.Pos.Tests.Hardware.Devices;

public class CustomerDisplayDataTests
{
    private static CustomerDisplayData Sample() => new()
    {
        ItemName = "宫保鸡丁",
        Price = 32.00m,
        Qty = 2,
        Total = 128.00m,
        Received = 140.00m,
        Change = 12.00m,
        Message = "欢迎光临",
    };

    [Fact]
    public void FormatAmountRow_Is_20_Columns_RightAligned()
    {
        var display = new VfdCustomerDisplay("COM1");
        string row = display.FormatAmountRow(Sample());

        Assert.Equal(20, row.Length);
        Assert.EndsWith("12.00", row); // 金额在右侧
    }

    [Fact]
    public void FormatAmountRow_AsciiMode_Uses_EnglishLabels()
    {
        var display = new VfdCustomerDisplay("COM1", encoding: VfdTextEncoding.Ascii);
        string row = display.FormatAmountRow(Sample());

        Assert.Contains("Tot:", row);
        Assert.Contains("Chg:", row);
        Assert.DoesNotContain("?", row);
    }

    [Fact]
    public void FormatAmountRow_Gb18030Mode_Uses_ChineseLabels()
    {
        var display = new VfdCustomerDisplay("COM1", encoding: VfdTextEncoding.Gb18030);
        string row = display.FormatAmountRow(Sample());

        Assert.Contains("合计:", row);
        Assert.Contains("找零:", row);
    }

    [Fact]
    public void TruncateByColumns_LongItemName_CutTo20()
    {
        string name = VfdCommandBuilder.TruncateByColumns("This is a very long product name over 20 cols", 20);
        Assert.Equal(20, name.Length);
        Assert.Equal("This is a very long ", name);
    }

    [Fact]
    public async Task Simulator_Works_Without_Hardware()
    {
        var sim = new CustomerDisplaySimulator();

        Assert.True(await sim.ConnectAsync());
        Assert.True(sim.IsConnected);

        var data = Sample();
        await sim.ShowAsync(data);

        Assert.Equal(1, sim.ShowCount);
        Assert.Same(data, sim.LastShown);
        Assert.Equal(128.00m, sim.LastShown!.Total);

        await sim.ClearAsync();
        Assert.Null(sim.LastShown);
        Assert.Equal(1, sim.ClearCount);
    }

    [Fact]
    public async Task Simulator_ShowAsync_Throws_OnNull()
    {
        var sim = new CustomerDisplaySimulator();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sim.ShowAsync(null!));
    }
}
