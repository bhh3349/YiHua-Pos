# 任务卡 T-103 · Linux 打印实现

> **收件人：MUSE-1** · 分支：`task/T-103-linux-printer`

---

## 0. 环境说明（先看这个，避免绕路）

| 项 | 值 |
|---|---|
| **唯一仓库** | `https://github.com/bhh3349/YiHua-Pos`（公开，不需要 token 就能读） |
| **基线** | 从 `main` 最新提交开始（当前 `8564612`） |
| **你的分支** | `task/T-103-linux-printer` |
| **推送** | `git push -u origin task/T-103-linux-printer` |

```bash
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-103-linux-printer
git log --oneline -1        # 确认基线是最新的
```

> ⚠️ 只有一个仓库，没有第二个。不要去找别的远程。

---

## 1. 项目背景

**逸华餐饮收银** —— 自有品牌餐饮 POS 系统。

| 项 | 内容 |
|---|---|
| 技术栈 | .NET 8 / .NET 6 双目标 + Avalonia UI 11 + Prism |
| 目标平台 | **Linux 触屏一体机（主力）** + Windows 台式机 |
| 硬件约束 | 2~4 核低功耗 CPU、4GB 内存、机械硬盘 |
| 性能目标 | 冷启动 ≤8s、内存 ≤300MB、安装包 ≤250MB |

**关键认知**：收银小票打印机走 **ESC/POS 标准指令集**，
**不需要厂商驱动**，只要能往设备发字节就能打印。

---

## 2. 铁律（违反会导致多 AI 协作撞车）

```
✅ 你只能修改这两个目录：
      client/src/YiHua.Pos.Hardware/Platforms/Linux/**
      client/tests/YiHua.Pos.Tests/Hardware/Linux/**

❌ 禁止修改（冻结文件，由 Lead 维护）：
      client/YiHua.Pos.sln
      client/Directory.Build.props
      client/src/YiHua.Pos.Domain/**
      client/src/YiHua.Pos.Hardware/Abstractions/**     ← 接口在这里，只能读
      client/src/YiHua.Pos.Hardware/YiHua.Pos.Hardware.csproj
      client/src/YiHua.Pos.Hardware/Printing/EscPos/**  ← T-102 已完成，不要动
      client/src/YiHua.Pos.Hardware/Scales/**           ← 别人负责
      client/tests/YiHua.Pos.Tests/YiHua.Pos.Tests.csproj
      其他任何模块

❌ 不复制任何第三方代码，只依据公开标准实现
❌ 不引入任何第三方打印库
❌ 不直接推 main
```

**需要改冻结文件时**：不要自己改，把需求写清楚反馈给 Lead。

---

## 3. 任务目标

实现 Linux 平台的四条打印通路 + 设备发现。

**依赖**：T-102（ESC/POS 指令生成器）**已完成**，你直接用它构造字节流。

---

## 4. 已冻结的接口

`client/src/YiHua.Pos.Hardware/Abstractions/HardwareAbstractions.cs`：

```csharp
public enum PrinterConnection { Usb, Serial, Network, Bluetooth }

public class PrinterStatus
{
    public bool IsConnected { get; set; }
    public bool IsOnline { get; set; }
    public bool HasPaper { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; }
    public int PaperWidthMm { get; set; }
}

public class PrinterInfo
{
    public string Name { get; set; }
    public PrinterConnection Connection { get; set; }
    public string Address { get; set; }   // USB: VID_PID / Serial: ttyUSB0 / Network: ip:port
    public string Model { get; set; }
}

public interface IReceiptPrinter
{
    string Name { get; }
    PrinterConnection Connection { get; }
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task PrintAsync(byte[] escposData, CancellationToken ct = default);
    Task<bool> TestAsync(CancellationToken ct = default);
    Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default);
    Task OpenCashDrawerAsync(CancellationToken ct = default);
}

public interface IPrinterDiscovery
{
    Task<IReadOnlyList<PrinterInfo>> DiscoverAsync(CancellationToken ct = default);
}
```

---

## 5. 交付物

```
client/src/YiHua.Pos.Hardware/Platforms/Linux/
├── LinuxSerialPrinter.cs       串口（/dev/ttyUSB*、/dev/ttyS*）
├── LinuxUsbPrinter.cs          USB（/dev/usb/lp* 或 libusb）
├── LinuxNetworkPrinter.cs      网口（TCP 9100）
├── LinuxBluetoothPrinter.cs    蓝牙（RFCOMM，/dev/rfcomm*）
├── LinuxPrinterDiscovery.cs    设备发现
├── LinuxSerialPortEnumerator.cs 枚举可用串口
└── UdevRulesGenerator.cs       生成 udev 规则文本（见第 7 节）

client/tests/YiHua.Pos.Tests/Hardware/Linux/
├── LinuxNetworkPrinterTests.cs
├── LinuxSerialPrinterTests.cs
├── PrinterDiscoveryTests.cs
└── UdevRulesGeneratorTests.cs
```

---

## 6. 技术要求

| 项 | 要求 |
|---|---|
| 目标框架 | `net8.0` + `net6.0` **都必须能编译** |
| 串口 | 用 `System.IO.Ports.SerialPort`（包已引用） |
| 网口 | `TcpClient`，默认端口 **9100** |
| USB | 优先 `/dev/usb/lp*`（内核 usblp 驱动）；没有则考虑 libusb |
| 测试 | **不能依赖真实硬件** —— 用抽象/接口隔离 IO，纯逻辑部分必须可单测 |

### 6.1 关键设计：IO 与逻辑分离

```
LinuxNetworkPrinter
  ├── 构造 ESC/POS 字节流（调用 T-102 的 EscPosBuilder）
  ├── 连接管理（重连、超时）
  └── 写入 socket

测试策略：
  - 把 socket 抽象成 IByteChannel，测试注入假实现
  - 断言「发出去的字节序列」而不是「打印机真的打了」
```

### 6.2 串口自动探测

```
遍历 /dev/ttyUSB* /dev/ttyS* /dev/ttyACM*
  → 打开
  → 发送 ESC/POS 状态查询指令（DLE EOT n）
  → 等 ≤300ms 看有没有回包
  → 有回包 = 是打印机
```

---

## 7. udev 规则（重要）

Linux 下普通用户默认**无权访问** USB/串口设备。必须提供 udev 规则，
让收银程序无需 root 就能用打印机。

**交付要求**：生成一份规则文本，内容形如：

```
# /etc/udev/rules.d/99-yihua-pos.rules
# USB 打印机
SUBSYSTEM=="usb", ATTRS{idVendor}=="<VID>", MODE="0666", GROUP="yihua"
# 串口设备
KERNEL=="ttyUSB*", MODE="0666", GROUP="yihua"
KERNEL=="ttyACM*", MODE="0666", GROUP="yihua"
```

`UdevRulesGenerator` 要能：
- 输出完整规则文本
- 支持传入已知的 VID/PID 列表
- 有单元测试断言输出内容

---

## 8. 验收标准

```
□ dotnet build client/YiHua.Pos.sln 通过（net8.0 + net6.0）
□ dotnet test 全通过
□ 单元测试覆盖：
    - 网口：字节流正确写入（用假 socket）
    - 串口：参数配置正确（波特率/数据位/校验位）
    - 发现：能识别出「假打印机」并排除普通串口设备
    - udev 规则文本生成正确
    - 断线重连逻辑
□ 没有第三方依赖
□ 平台代码只在 Platforms/Linux/ 内
```

---

## 9. 参考

- **ESC/POS 是爱普生公开标准**，指令集可自行查证
- T-102 已实现的 `EscPosBuilder` 在 `client/src/YiHua.Pos.Hardware/Printing/EscPos/`
- Linux 串口文档：`man termios`、`man tty`
- udev 文档：`man udev`、`man udevadm`

---

## 10. 完成后

```bash
dotnet build client/YiHua.Pos.sln
dotnet test  client/YiHua.Pos.sln

git fetch origin && git rebase origin/main
git add -A
git commit -m "T-103: 实现 Linux 打印通路"
git push -u origin task/T-103-linux-printer
```

告诉 Lead：「T-103 完成，分支 task/T-103-linux-printer」
附：改动文件清单 + dotnet test 输出 + udev 规则示例
