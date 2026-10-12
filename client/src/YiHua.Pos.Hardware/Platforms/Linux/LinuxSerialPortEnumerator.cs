namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// Linux 串口设备枚举：列出 /dev 下的 ttyUSB*、ttyS*、ttyACM*。
/// 纯逻辑（只读目录），可单测。
/// </summary>
public static class LinuxSerialPortEnumerator
{
    private static readonly string[] Patterns = { "ttyUSB*", "ttyS*", "ttyACM*" };

    /// <summary>枚举本机串口设备（默认 /dev）</summary>
    public static IReadOnlyList<string> GetSerialPorts() => GetSerialPorts("/dev");

    /// <summary>
    /// 枚举指定目录下的串口设备；目录不存在返回空列表。
    /// （暴露目录参数以便单测用临时目录验证匹配逻辑）
    /// </summary>
    public static IReadOnlyList<string> GetSerialPorts(string devDirectory)
    {
        var result = new List<string>();
        if (!Directory.Exists(devDirectory))
            return result;

        foreach (var pattern in Patterns)
        {
            try
            {
                result.AddRange(Directory.GetFiles(devDirectory, pattern));
            }
            catch
            {
                // 忽略单个 pattern 的错误（如权限不足），继续其它 pattern
            }
        }
        return result.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
    }
}
