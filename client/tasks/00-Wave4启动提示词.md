# Wave 4 启动提示词

> Wave 3 已完成并合并：登录 / 桌台 / 订单管理 / 正餐点单 / 商品 / 结账。
> 当前 main：**444 个测试全绿**。
> 复制对应段落发给对应的人。

---

## 通用信息

| 项 | 值 |
|---|---|
| **唯一仓库** | `https://github.com/bhh3349/YiHua-Pos` |
| **基线** | `main` 最新（`2cab19f`） |
| **本地路径（Hermes）** | `C:\WorkSpace\purge\yihua-pos` |

## Wave 4 分工

| 谁 | 任务 | 分支 | 独占目录 |
|---|---|---|---|
| **仓鼠1号** | T-309 交班管理 | `task/T-309-shift` | `UI/Views/Shift/`, `UI/ViewModels/Shift/`, `UI/Services/Shift/` |
| **仓鼠2号** | T-304 快餐模式 | `task/T-304-fastfood` | `UI/Views/FastFood/`, `UI/ViewModels/FastFood/`, `UI/Services/FastFood/` |
| **仓鼠3号** | T-310 经营报表 | `task/T-310-report` | `UI/Views/Report/`, `UI/ViewModels/Report/`, `UI/Services/Report/` |
| **Hermes** | T-405 团购对接 | `task/T-405-groupbuy` | `server/**` |

**目录互不重叠，可以完全并行。**

---

## 三条新规矩（Wave 4 必读）

### ① 命名空间撞名（Wave 3 真实踩过）

`ViewModels/` 下建了 `Desk/` 和 `Product/` 目录后，会生成同名子命名空间，
导致其他模块写 `Desk` / `Product` 时报：

```
error CS0118: 「Desk」是命名空间，但此处被当做类型来使用
```

**Wave 4 的目录名**：`Shift/` `FastFood/` `Report/`

- `FastFood` `Report` 安全
- **`Shift` 有风险**：Domain 里现在没有 `Shift` 实体，但 T-309 要新增一个，
  加进去就会撞名。**建议目录改叫 `Shifts/`**，或用文件内别名（见任务卡第 1 节）

**如果遇到 CS0118，别名必须写在 `namespace` 语句之后**：
```csharp
namespace YiHua.Pos.UI.ViewModels.Shift;

// 放这里，不要放文件顶部
using ShiftEntity = YiHua.Pos.Domain.Entities.Shift;
```
文件顶部的 `using` 属于全局作用域，更近的作用域优先，全局别名会被跳过。

**不要建** `Member/` `Payment/` `Order/` `Desk/` `Product/` 这些目录（同名实体已存在）。

### ② Domain 缺口一律「上报」，不要自己改

`client/src/YiHua.Pos.Domain/**` 是**冻结**的。
Shift 实体、取号记录、配送信息、团购字段等都不在里面。

**正确做法**：在报告里列「Domain 缺口清单」，我统一加。
**错误做法**：自己改 Domain —— 4 个人同时改必撞车。

### ③ 提交前先 commit

Hermes 做 T-202/203 时，改动全在工作区没提交，报告说「完成」。
**说「完成」之前必须 `git commit` + `git push`。**

---

# ① 给仓鼠1号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠1号。
Wave 4 你负责交班管理（T-309）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-309-shift
git pull origin main

## 任务卡
client/tasks/T-309_交班_仓鼠1号.md

## 一句话
交班是账务分界线：开班交备用金 → 班中实时统计 → 交班对账 → 差额必填原因。

## 重点
- 差额数字要最醒目，且区分短款（警告色）和溢余（中性色）
- 对账页面左右分栏：系统数据 vs 实点数据
- 必须明确的计算规则（写进报告并测）：退菜退现金属不算、冲正单怎么算、
  抹零归类、记账收回算哪班、备用金不足算短款
- ShiftCalculator 是纯逻辑类，重点测
- 强制交班仅店长 + 二次确认 + 记录操作人
- 未交班时要阻塞级拦截下一个收银员

## Domain 缺口（不要自己改，在报告里列）
Shift 实体 / IShiftRepository / 交班差异记录 / 备用金字段

## 铁律
✅ 只改 UI/Views/Shift/ UI/ViewModels/Shift/ UI/Services/Shift/ 及对应测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln
❌ 不加第三方库

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-309: 交班管理"
git push -u origin task/T-309-shift

报告：改动清单 + 测试输出 + Domain 缺口 + 计算规则说明
```

---

# ② 给仓鼠2号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠2号。
Wave 4 你负责快餐模式（T-304）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-304-fastfood
git pull origin main

## 任务卡
client/tasks/T-304_快餐_仓鼠2号.md

## 一句话
快餐 = 没桌台，取号点单，先付后吃。叫号屏投副屏。

## 重点
- 取号：按日序号发号，叫号/跳号/插队/退号/日切重置
- 快餐是「先付后吃」：未结账不能放走
- 就餐方式三选一：堂食/外带（餐具数量）/配送（地址电话）
- 叫号屏字号必须够大（3 米外能看清），不要为了好看缩小
- 叫号屏要投副屏（T-106 的 SecondaryScreenManager），副屏没接要降级到主屏并提示
- 复用 T-303 已有的控件，不要重造

## 命名空间注意
不要建 ViewModels/TakeNo/ 这类目录。
如果命名空间和 Domain 实体同名会报 CS0118。本卡用 FastFood。

## Domain 缺口（不要自己改，在报告里列）
发号器 / 取号记录实体 / 配送地址电话 / 餐具数量

## 铁律
✅ 只改 UI/Views/FastFood/ UI/ViewModels/FastFood/ UI/Services/FastFood/ 及测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-304: 快餐模式（取号点单）"
git push -u origin task/T-304-fastfood

报告：改动清单 + 测试输出 + Domain 缺口 + 取号状态机图 + 叫号屏降级方案
```

---

# ③ 给仓鼠3号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠3号。
Wave 4 你负责经营报表（T-310）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-310-report
git pull origin main

## 任务卡
client/tasks/T-310_报表_仓鼠3号.md

## 一句话
给不懂财务的老板看的报表：今天卖了多少、卖得最好的是什么、为什么比昨天少。

## 重点
- 不引入第三方图表库（铁律）。条形图自己用 Border 宽度画
- 占比用条形不用饼图（小屏难比较长度）
- 今日概览：营业额/订单数/客单价/优惠/实收 + 较昨日同比
- 时段分析（餐饮最重要，决定备货和排班），标注高峰时段
- 菜品排行按销售额（老板关心赚多少），可下钻
- 桌台统计：桌次/金额/平均客单价，看利用率
- CSV 导出必须带 UTF-8 BOM（Excel 打开中文不乱码），中文表头

## 必须明确的计算规则（写进报告并测）
退菜算不算营业额、冲正单怎么算、抹零算不算、
客单价分母用订单数还是件数、作废单排除、跨天订单归属。

## 边界（重点）
**占比的分母为 0 时必须返回 0，不能抛异常、不能返回 NaN**
（今天还没开张是很常见的真实场景）

## Domain 缺口
报表数据量大，不应该全量拉到客户端本地算。
本期做本地 SQLite 聚合 + IReportDataSource 接口抽象（预留切服务端）。
在报告里说明长期需要服务端报表接口。

## 铁律
✅ 只改 UI/Views/Report/ UI/ViewModels/Report/ UI/Services/Report/ 及测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln
❌ 不引入第三方图表库（会被护栏拦）

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-310: 经营报表"
git push -u origin task/T-310-report

报告：改动清单 + 测试输出 + 计算规则说明 + Domain缺口/服务端报表接口需求
```

---

# ④ 给 Hermes

```
唯一协作仓库：https://github.com/bhh3349/YiHua-Pos
旧的 bhh3349/SAAS 已废弃，只读归档。

cd C:\WorkSpace\purge\yihua-pos
git fetch origin
git checkout -b task/T-405-groupbuy origin/main

任务卡：client/tasks/T-405_团购_Hermes.md

Wave 4 你负责团购平台对接（服务端）。

重点：
- **adapter 架构**：业务代码不出现 if platform == "meituan"，
  加新平台只加一个 adapter 文件不改业务代码（用测试证明这点）
- 本期只实现 self_platform 完整逻辑，其余平台留骨架返回「平台未接入」
- 8 种券状态错误码要细分，不能笼统返回「券无效」
- 核销幂等：并发下同一张券只能成功一次
- 全程 Decimal，禁止 float
- 密钥不落日志

只改 server/**。models.py 冻结，确实要改先在报告里说明。

⚠️ 做完必须 commit 再报完成（T-202/203 那次全在工作区没提交，差点白干）。

报告：改动清单 + 测试输出 + adapter 架构说明 + 券状态错误码表
     + 客户端需要配合的字段与界面清单
```

---

# Lead 验收流程

```
1. 越界检查（只能改自己的独占目录）
2. 全量编译 net8.0 + net6.0
3. 全量测试（期望 444 + 新增全绿）
4. python tools/guardrails.py（16 项）
5. 代码质量抽查（不只看测试数）
6. 合并 --no-ff 到 main
```

**三个常见坑**：
```
CI 报红        → git fetch && git rebase origin/main
CS0118 撞名    → 别名要写在 namespace 之后，不是文件顶部
护栏报红       → 那是护栏在工作，别绕过，按提示改
```