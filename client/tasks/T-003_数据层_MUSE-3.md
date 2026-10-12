# 任务卡 T-003 · 本地 SQLite 数据层

> **收件人：MUSE-3** · 项目：逸华餐饮收银 · 工作区：`C:\WorkSpace\purge\client`

---

## 0. 项目背景

**逸华餐饮收银** —— 自有品牌餐饮 POS 系统。

| 项 | 内容 |
|---|---|
| 技术栈 | .NET 8 / .NET 6 双目标 + Avalonia UI 11 + Prism |
| 目标平台 | **Linux 触屏一体机（主力）** + Windows 台式机 |
| 硬件约束 | 2~4 核低功耗 CPU、4GB 内存、机械硬盘 |

**核心架构决策：本地优先（Local-First）**

```
用户的每一步操作 ──→ 先写本地 SQLite（权威数据源）
                      ↓
                  断网也能完整收银
                      ↓
                  写 outbox 队列，联网后补传
```

**你的任务是这个架构的地基** —— 本地数据层。

---

## 1. 铁律（违反会导致多 AI 协作撞车）

```
✅ 你只能修改这个目录下的文件：
      client/src/YiHua.Pos.Data/**
      client/tests/YiHua.Pos.Tests/Data/**

❌ 禁止修改以下任何文件（已冻结）：
      YiHua.Pos.sln / Directory.Build.props
      client/src/YiHua.Pos.Domain/**          ← 实体与接口在这里，只能读
      client/src/YiHua.Pos.Hardware/**
      其他任何模块

❌ 禁止引入 SqlSugar / EF Core 之外的重型 ORM
```

---

## 2. 任务目标

实现本地 SQLite 数据层，**实现 `Domain/Interfaces/IRepositories.cs` 里的全部接口**。

---

## 3. 已冻结的接口

`client/src/YiHua.Pos.Domain/Interfaces/IRepositories.cs`：

```csharp
IOrderRepository        订单（增删改查 + 按日期/桌台/班次查询 + 未同步查询）
IProductRepository      商品（全量/按分类/条码/模糊搜索/批量替换）
ICategoryRepository     分类
IUnitRepository         单位
IDeskRepository         桌台 + 区域
IShiftRepository        交班
IOutboxRepository       离线补传队列
IDictionaryRepository   通用键值配置
IDbInitializer          建表 / 备份
```

实体定义在 `client/src/YiHua.Pos.Domain/Entities/`（Order / Product / Desk / Shift / OutboxItem）。

---

## 4. 交付物

```
client/src/YiHua.Pos.Data/
├── YiHua.Pos.Data.csproj
├── PosDbContext.cs                 数据库上下文
├── DbInitializer.cs                建表 + 索引
├── SqlitePaths.cs                  跨平台路径解析
├── Repositories/
│   ├── OrderRepository.cs
│   ├── ProductRepository.cs
│   ├── CategoryRepository.cs
│   ├── UnitRepository.cs
│   ├── DeskRepository.cs
│   ├── ShiftRepository.cs
│   ├── OutboxRepository.cs
│   └── DictionaryRepository.cs
└── Migrations/
    └── V001_Init.sql               建表脚本

client/tests/YiHua.Pos.Tests/Data/
├── OrderRepositoryTests.cs
├── OutboxRepositoryTests.cs
└── DbInitializerTests.cs
```

---

## 5. 技术要求

| 项 | 要求 |
|---|---|
| ORM | **SqlSugar**（`SqlSugarCore` 包，版本见 `Directory.Build.props`） |
| 数据库 | SQLite，**WAL 模式**（断电保护） |
| 目标框架 | `net8.0` + `net6.0` 双目标必须编译通过 |
| 路径 | 跨平台：Windows `%LOCALAPPDATA%\YiHua\`；Linux `~/.local/share/YiHua/` 或 `/data/` |
| 索引 | 订单表按 `clientOrderNo`（唯一）、`createdAt`、`deskId`、`shiftId` 建索引 |
| 事务 | 下单（订单+明细+支付）必须在一个事务里 |

### 关键设计点

1. **幂等键**：`Order.ClientOrderNo` 必须有唯一索引（断网补传去重的基础）
2. **WAL 模式**：`PRAGMA journal_mode=WAL` —— 断电不损坏
3. **批量替换**：商品同步用「先删后插」+ 事务，避免中间态
4. **outbox 顺序**：按 `createdAt` 排序取出，保证补传顺序
5. **时间格式**：统一 `yyyy-MM-dd HH:mm:ss` 字符串存储（SQLite 友好）

---

## 6. 验收标准

```
□ dotnet build 通过（net8.0 + net6.0）
□ dotnet test 全通过
□ 首次启动自动建库建表建索引
□ 测试覆盖：
    - 订单插入（含明细+支付）在事务里，失败全回滚
    - clientOrderNo 唯一约束生效（重复插入抛异常）
    - outbox 按时间顺序取出
    - 商品批量替换后数量正确
    - 数据库文件路径跨平台正确
□ 提供 BackupAsync 实现
```

---

## 7. 提交要求

```
1. 改动文件清单
2. dotnet build + dotnet test 输出
3. 数据库表结构说明（字段 + 索引）
```

---

## 8. 参考（只看不抄）

参考实现的本地表结构（**仅供理解字段含义**）：
```
C:\WorkSpace\purge\work\decomp\Cashier.Domain\Cashier.Domain.Entities\**  实体定义
C:\WorkSpace\purge\pos\docs\02-API契约与数据模型.md                       表结构文档
```

---

## 9. 完成后

反馈给 Lead，通过后进入 Wave 2：
```
你的下一个任务：T-005 通用控件库（Avalonia 收银界面控件）
```
