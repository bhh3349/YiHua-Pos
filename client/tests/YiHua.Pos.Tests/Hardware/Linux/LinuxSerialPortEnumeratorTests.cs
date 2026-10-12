using YiHua.Pos.Hardware.Platforms.Linux;
using Xunit;

namespace YiHua.Pos.Tests.Hardware.Linux;

/// <summary>串口枚举器测试：用临时目录验证文件名匹配逻辑，不碰真实 /dev。</summary>
public class LinuxSerialPortEnumeratorTests
{
    [Fact]
    public void GetSerialPorts_Returns_Matching_Devices_Only()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "yihua-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "ttyUSB0"), string.Empty);
            File.WriteAllText(Path.Combine(tmp, "ttyACM1"), string.Empty);
            File.WriteAllText(Path.Combine(tmp, "ttyS2"), string.Empty);
            File.WriteAllText(Path.Combine(tmp, "sda"), string.Empty);      // 不匹配
            File.WriteAllText(Path.Combine(tmp, "tty"), string.Empty);      // 不匹配

            var ports = LinuxSerialPortEnumerator.GetSerialPorts(tmp);

            Assert.Equal(3, ports.Count);
            Assert.Contains(Path.Combine(tmp, "ttyUSB0"), ports);
            Assert.Contains(Path.Combine(tmp, "ttyACM1"), ports);
            Assert.Contains(Path.Combine(tmp, "ttyS2"), ports);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    [Fact]
    public void GetSerialPorts_Missing_Directory_Returns_Empty()
    {
        var ports = LinuxSerialPortEnumerator.GetSerialPorts("/no/such/dir/yihua-test");

        Assert.Empty(ports);
    }

    [Fact]
    public void GetSerialPorts_Results_Are_Sorted()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "yihua-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "ttyUSB1"), string.Empty);
            File.WriteAllText(Path.Combine(tmp, "ttyUSB0"), string.Empty);

            var ports = LinuxSerialPortEnumerator.GetSerialPorts(tmp);

            Assert.Equal(
                new[] { Path.Combine(tmp, "ttyUSB0"), Path.Combine(tmp, "ttyUSB1") },
                ports.ToArray());
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }
}
