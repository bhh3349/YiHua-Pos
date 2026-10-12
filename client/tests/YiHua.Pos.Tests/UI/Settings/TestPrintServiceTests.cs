using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;
using System.Text;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.UI.Services.Settings;
using Xunit;

namespace YiHua.Pos.Tests.UI.Settings;

/// <summary>
/// 测试打印服务测试。
///
/// 这个功能直接决定客服成本：商家配完打印机要能自己打一张纸确认，
/// 而不是打电话问「我这样配对不对」。
/// </summary>
public class TestPrintServiceTests
{
    private sealed class FakePrinter : IReceiptPrinter
    {
        public FakePrinter(bool connectOk = true, Exception? throwOnPrint = null)
        {
            ConnectOk = connectOk;
            ThrowOnPrint = throwOnPrint;
        }

        public string Name { get; set; } = "测试打印机";
        public PrinterConnection Connection => PrinterConnection.Usb;
        public bool ConnectOk { get; }
        public Exception? ThrowOnPrint { get; }
        public byte[]? LastPayload { get; private set; }
        public bool Disconnected { get; private set; }

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(ConnectOk);

        public Task DisconnectAsync()
        {
            Disconnected = true;
            return Task.CompletedTask;
        }

        public Task PrintAsync(byte[] escposData, CancellationToken ct = default)
        {
            if (ThrowOnPrint is not null) throw ThrowOnPrint;
            LastPayload = escposData;
            return Task.CompletedTask;
        }

        public Task<bool> TestAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<YiHua.Pos.Hardware.Abstractions.PrinterStatus> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new YiHua.Pos.Hardware.Abstractions.PrinterStatus { IsConnected = true });
        public Task OpenCashDrawerAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly TestPrintService Svc = new();

    [Fact]
    public async Task PrintTestPage_Succeeds_And_Sends_Payload()
    {
        var printer = new FakePrinter();

        var (ok, error) = await Svc.PrintTestPageAsync(printer, 80);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(printer.LastPayload);
        Assert.True(printer.LastPayload!.Length > 0);
    }

    [Fact]
    public async Task Connect_Fails_Returns_Human_Readable_Error()
    {
        var printer = new FakePrinter(connectOk: false);

        var (ok, error) = await Svc.PrintTestPageAsync(printer, 80);

        Assert.False(ok);
        Assert.NotNull(error);
        // 商家要看到「怎么解决」，不能只看到一个 false
        Assert.Contains("USB", error);
    }

    [Fact]
    public async Task Print_Throws_Returns_Human_Readable_Error()
    {
        var printer = new FakePrinter(throwOnPrint: new IOException("设备无响应"));

        var (ok, error) = await Svc.PrintTestPageAsync(printer, 58);

        Assert.False(ok);
        Assert.Contains("缺纸", error); // 给的是可执行的建议，不是原始异常
    }

    [Fact]
    public async Task Null_Printer_Returns_Error()
    {
        var (ok, error) = await Svc.PrintTestPageAsync(null!, 80);

        Assert.False(ok);
        Assert.Equal("请先选择打印机", error);
    }

    [Theory]
    [InlineData(58, 32)]
    [InlineData(80, 48)]
    public void ColumnsFor_Matches_PaperWidth(int mm, int expected)
    {
        Assert.Equal(expected, TestPrintContent.ColumnsFor(mm));
    }

    [Fact]
    public void Build_58mm_Lines_Within_32_Columns()
    {
        string text = Encoding.UTF8.GetString(TestPrintContent.Build(58));

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            Assert.True(TestPrintContent.DisplayWidth(trimmed) <= 32,
                $"58mm 版式超宽（{TestPrintContent.DisplayWidth(trimmed)} 列）：{trimmed}");
        }
    }

    [Fact]
    public void Build_80mm_Lines_Within_48_Columns()
    {
        string text = Encoding.UTF8.GetString(TestPrintContent.Build(80));

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            Assert.True(TestPrintContent.DisplayWidth(trimmed) <= 48,
                $"80mm 版式超宽（{TestPrintContent.DisplayWidth(trimmed)} 列）：{trimmed}");
        }
    }

    [Fact]
    public void DisplayWidth_Counts_Cjk_As_Two()
    {
        Assert.Equal(4, TestPrintContent.DisplayWidth("中文"));   // 2 汉字 × 2
        Assert.Equal(4, TestPrintContent.DisplayWidth("ABCD"));   // 4 ASCII × 1
        Assert.Equal(4, TestPrintContent.DisplayWidth("中AB"));   // 1 汉字 × 2 + 2 ASCII × 1
        Assert.Equal(0, TestPrintContent.DisplayWidth(""));
        Assert.Equal(0, TestPrintContent.DisplayWidth(null));
    }

    [Fact]
    public void Center_Positions_Text_In_Middle()
    {
        // AB 占 2 列，在 10 列里居中：左边补 4 空格，无法对称补齐时靠左
        string line = TestPrintContent.Center("AB", 10);

        Assert.Equal("    AB", line);
        Assert.Equal(6, TestPrintContent.DisplayWidth(line));

        // 居中只在左边补空格，不在右边补（对称留白对小票没有意义）
        Assert.Equal("    中文", TestPrintContent.Center("中文", 12));
    }

    [Fact]
    public void Center_Text_Wider_Than_Paper_Not_Truncated()
    {
        string line = TestPrintContent.Center("超出宽度的长文本", 4);

        Assert.Equal("超出宽度的长文本", line);
    }

    [Theory]
    [InlineData(58, "58mm")]
    [InlineData(80, "80mm")]
    public void Build_Shows_Paper_Width_For_Troubleshooting(int mm, string expect)
    {
        // 商家要能一眼确认纸宽选对了没 —— 选错纸宽打印必然错位
        string text = Encoding.UTF8.GetString(TestPrintContent.Build(mm));

        Assert.Contains(expect, text);
        Assert.Contains("打印测试", text);
    }
}