using System.Text;

namespace YiHua.Pos.Hardware.Platforms.Linux;

/// <summary>
/// udev 规则生成器：Linux 下普通用户默认无权访问 USB/串口设备，
/// 用这份规则让收银程序无需 root 即可使用打印机。
/// 生成的文本写入 /etc/udev/rules.d/99-yihua-pos.rules 后重载 udev 生效。
/// 纯文本生成逻辑，可单测。
/// </summary>
public static class UdevRulesGenerator
{
    /// <summary>规则文件名</summary>
    public const string RulesFileName = "99-yihua-pos.rules";

    /// <summary>设备组名（收银程序运行用户需加入该组）</summary>
    public const string DefaultGroup = "yihua";

    /// <summary>
    /// 生成完整 udev 规则文本。
    /// </summary>
    /// <param name="knownUsbPrinters">已知 USB 打印机的 (VID, PID) 列表，如 ("0416", "5011")</param>
    /// <param name="group">设备组名，默认 yihua</param>
    public static string Generate(
        IEnumerable<(string Vid, string Pid)>? knownUsbPrinters = null,
        string group = DefaultGroup)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# /etc/udev/rules.d/99-yihua-pos.rules");
        sb.AppendLine("# 逸华餐饮收银：打印机设备权限规则（普通用户无需 root 即可访问）");
        sb.AppendLine("# 安装：sudo cp 99-yihua-pos.rules /etc/udev/rules.d/ && sudo udevadm control --reload-rules && sudo udevadm trigger");
        sb.AppendLine();
        sb.AppendLine("# USB 打印机设备节点（内核 usblp 驱动）");
        sb.AppendLine($"KERNEL==\"lp*\", SUBSYSTEM==\"usb\", MODE=\"0666\", GROUP=\"{group}\"");

        var printers = knownUsbPrinters?.ToList() ?? new List<(string Vid, string Pid)>();
        if (printers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# 已知 USB 打印机（按 VID/PID 精确匹配）");
            foreach (var (vid, pid) in printers)
            {
                sb.AppendLine(
                    $"SUBSYSTEM==\"usb\", ATTRS{{idVendor}}==\"{vid.ToLowerInvariant()}\", " +
                    $"ATTRS{{idProduct}}==\"{pid.ToLowerInvariant()}\", MODE=\"0666\", GROUP=\"{group}\"");
            }
        }

        sb.AppendLine();
        sb.AppendLine("# USB 转串口 / USB CDC 串口（打印机、电子秤常用）");
        sb.AppendLine($"KERNEL==\"ttyUSB*\", MODE=\"0666\", GROUP=\"{group}\"");
        sb.AppendLine($"KERNEL==\"ttyACM*\", MODE=\"0666\", GROUP=\"{group}\"");
        return sb.ToString();
    }
}
