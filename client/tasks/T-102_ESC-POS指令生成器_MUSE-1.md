# 任务卡 T-102 · ESC/POS 指令生成器

> **收件人：MUSE-1** · 项目：逸华餐饮收银 · 工作区：`（略）

---

## 0. 项目背景

**逸华餐饮收银** —— 自有品牌餐饮 POS 系统。

| 项 | 内容 |
|---|---|
| 技术栈 | .NET 8 / .NET 6 双目标 + Avalonia UI 11 + Prism |
| 目标平台 | **Linux 触屏一体机（主力）** + Windows 台式机 |
| 硬件约束 | 2~4 核低功耗 CPU、4GB 内存、机械硬盘 |
| 性能目标 | 冷启动 ≤8s、内存 ≤300MB、安装包 ≤250MB |

**关键认知**：收银小票打印机走 **ESC/POS 标准指令集**（爱普生标准，所有厂商兼容），
**不需要厂商驱动**，只要能往设备发字节就能打印。你的任务就是把指令生成器做出来。

---

## 1. 铁律（违反会导致多 AI 协作撞车）

```
✅ 你只能修改这个目录下的文件：
      src/YiHua.Pos.Hardware/Printing/EscPos/**
      tests/YiHua.Pos.Tests/Hardware/EscPos/**

❌ 禁止修改以下任何文件（已冻结，由 Lead 维护）：
      YiHua.Pos.sln
      Directory.Build.props
      src/YiHua.Pos.Domain/**
      src/YiHua.Pos.Hardware/Abstractions/**
      src/YiHua.Pos.Hardware/YiHua.Pos.Hardware.csproj
      其他任何模块

❌ 禁止复制行业通行的代码（可以看，但要自己写）
❌ 禁止引入任何第三方打印库（自己实现指令）
```

**如果发现需要改冻结文件**：不要自己改，把需求写清楚反馈给 Lead。

---

## 2. 任务目标

实现一个纯 C# 的 **ESC/POS 指令生成器**，业务层用它构造打印数据。

---

## 3. 交付物

```
src/YiHua.Pos.Hardware/Printing/EscPos/
├── EscPosBuilder.cs        Fluent API 构建器
├── EscPosCommands.cs       指令常量表
├── EscPosConstants.cs      字符编码/对齐/字号等枚举
└── EscPosImageConverter.cs 位图转 ESC/POS 点阵

tests/YiHua.Pos.Tests/Hardware/EscPos/
└── EscPosBuilderTests.cs   单元测试
```

### 必须实现的 API

```csharp
public class EscPosBuilder
{
    EscPosBuilder Init();                              // 初始化打印机
    EscPosBuilder Align(AlignStyle style);             // 对齐（左/中/右）
    EscPosBuilder Bold(bool on);                       // 加粗
    EscPosBuilder Underline(bool on);                  // 下划线
    EscPosBuilder FontSize(int width, int height);     // 字号（1-8 倍）
    EscPosBuilder Charset(CharsetType type);           // 字符集（中文必须支持 GB18030）
    EscPosBuilder Text(string text);                   // 文本
    EscPosBuilder TextLine(string text);               // 文本 + 换行
    EscPosBuilder Line();                              // 分隔线
    EscPosBuilder Feed(int lines = 1);                 // 走纸
    EscPosBuilder Cut(CutMode mode);                   // 切纸（全切/半切）
    EscPosBuilder OpenDrawer();                        // 开钱箱（走打印机 RJ11）
    EscPosBuilder QrCode(string data, int size = 6);   // 二维码
    EscPosBuilder Barcode(string data, BarcodeType t); // 一维码
    EscPosBuilder Image(byte[] pngBytes, int maxWidth); // 位图（logo）

    byte[] Build();                                    // 输出字节流
    static string ToHexString(byte[] data);            // 调试用
}
```

---

## 4. 技术要求

| 项 | 要求 |
|---|---|
| 目标框架 | `net8.0` + `net6.0` **双目标必须都能编译** |
| 依赖 | **零第三方依赖**（只用 BCL） |
| 中文 | 必须支持 GB18030 编码（`Encoding.GetEncoding("GB18030")`） |
| 位图 | 实现 1-bit 点阵转换（ESC * 或 GS v 0 指令） |
| 线程安全 | Builder 是实例级，不要求线程安全；但指令常量表要 `static readonly` |

### ESC/POS 关键指令（供参考，请自行核对规范）

```
ESC @        初始化
ESC a n      对齐（0左 1中 2右）
ESC E n      加粗
ESC - n      下划线
GS ! n       字号（高4位宽、低4位高）
ESC d n      走纸 n 行
GS V m       切纸
ESC p m t    开钱箱
GS ( k       二维码
GS k         一维码
```

---

## 5. 验收标准

```
□ dotnet build YiHua.Pos.sln 通过（net8.0 和 net6.0 都要过）
□ dotnet test 全通过
□ 单元测试覆盖：
    - 每个指令生成的字节序列与规范一致（断言具体字节）
    - 中文文本用 GB18030 编码
    - 位图转换输出尺寸正确
    - Fluent 链式调用能正确累积指令
□ 提供 ToHexString() 便于调试
□ 没有任何第三方依赖
```

---

## 6. 提交要求

完成后反馈：

```
1. 改动文件清单（完整路径）
2. dotnet build 输出（两个目标框架）
3. dotnet test 输出
4. 一个「打印一张完整小票」的示例代码
```

---

## 7. 参考

**ESC/POS 是爱普生公开的行业标准**，指令集可以自行查证。关键指令速查：

```
ESC @        初始化打印机          1B 40
ESC a n      对齐（0左 1中 2右）   1B 61 n
ESC E n      加粗                  1B 45 n
ESC - n      下划线                1B 2D n
GS  ! n      字号（高4位宽 低4位高） 1D 21 n
ESC d n      走纸 n 行             1B 64 n
GS  V m      切纸                  1D 56 m
ESC p m t    开钱箱                1B 70 m t
GS  ( k      二维码                1D 28 6B ...
GS  k        一维码                1D 6B ...
ESC * m nL nH 位图（1-bit 点阵）    1B 2A m nL nH <data>
```

**中文编码**：使用 GB18030（`Encoding.GetEncoding("GB18030")`）。

**注意**：只依据公开标准实现，不要照搬任何第三方代码。

---


## 8. 完成后

把结果反馈给 Lead（我），我会做集成检查：
```
1. 检查有没有越界修改
2. 全量编译 + 全量测试
3. 通过后进入 Wave 2（你下一个任务是 T-103 Linux 打印实现）
```
