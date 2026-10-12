# Wave 5 启动提示词

> Wave 4 已完成：快餐 / 交班 / 报表三个模块已合并，**547 测试全绿**。
> 当前 main：`3b52e7a`
> 复制对应段落发给对应的人。

---

## 通用信息

| 项 | 值 |
|---|---|
| **唯一仓库** | `https://github.com/bhh3349/YiHua-Pos` |
| **基线** | `main` 最新（`3b52e7a`，547 个测试） |
| **本地路径（Hermes）** | `C:\WorkSpace\purge\yihua-pos` |

## Wave 5 分工

**这一波全部是收银端**（用户明确要求：先把收银端开发好）。

| 谁 | 任务 | 分支 | 独占目录 |
|---|---|---|---|
| **仓鼠1号** | T-501 退款/退菜/冲正 | `task/T-501-refund` | `Views/Refund`, `ViewModels/Refund`, `Services/Refund` |
| **仓鼠2号** | T-502 挂单/取单/预结账 | `task/T-502-hangorder` | `Views/HangOrder`, `ViewModels/HangOrder`, `Services/HangOrder` |
| **仓鼠3号** | T-503 设置与设备管理 | `task/T-503-settings` | `Views/Settings`, `ViewModels/Settings`, `Services/Settings` |

**12 个独占目录，零重叠，可以完全并行。**

---

## Wave 5 的三条新规矩

### ① 命名空间撞名（Wave 3 真实踩过）

`ViewModels/` 下建了 `Desk/` 和 `Product/` 目录后，会生成同名子命名空间，
导致其他模块写 `Desk` / `Product` 时报：

```
error CS0118: 「Desk」是命名空间，但此处被当做类型来使用
```

**Wave 5 的 `Refund` `HangOrder` `Settings` 都不与实体重名，安全。**

**如果遇到 CS0118，别名必须写在 `namespace` 语句之后**：
```csharp
namespace YiHua.Pos.UI.ViewModels.Refund;

// 放这里，不要放文件顶部
using Xxx = YiHua.Pos.Domain.Entities.Xxx;
```
文件顶部的 `using` 属于全局作用域，更近的作用域优先，全局别名会被跳过。

### ② Domain 缺口一律「上报」，不要自己改

`client/src/YiHua.Pos.Domain/**` 是**冻结**的。

本波需要的 Domain 新增（**全部由我统一加**）：
- T-501：`RefundRecord` / `IRefundRepository` / `RefundType` / 部分退款状态
- T-502：`HangOrder` / `IHangOrderRepository` / `ParkedOrder` 状态 / 拆单关联
- T-503：`StoreSetting` / `PrinterConfig` / `SystemConfig` 及各自仓储

**在报告里列「Domain 缺口清单」即可。不要自己改。**

### ③ 提交前先 commit

**说「完成」之前必须 `git commit` + `git push`。**
改动全在工作区没提交却报完成，这个坑已经踩过一次。

---

## 本波的验收重点（三张卡各有"最容易被做错"的点）

| 任务 | 最容易做错的点 |
|---|---|
| T-501 | 退款金额红线（累计退款不能超实收）；冲正后**桌台要释放** |
| T-502 | 桌台"空闲"和"有挂单"必须是**两个独立状态**，不能合并 |
| T-503 | **诊断报告**（决定客服成本）；打印机**测试打印**（决定商家能否自助） |

---

# ① 给仓鼠1号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号仓鼠1号。
Wave 5 你负责结账后退款（T-501）。**这一波优先级最高。**

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-501-refund
git pull origin main

## 任务卡
client/tasks/T-501_退款_仓鼠1号.md

## 一句话
结账前的退菜 T-303 已能做，但结账后退菜 / 整单退单 / 冲正完全没有。
这是收银员每天都要用的操作。

## 重点
- **三个操作必须明确区分**：退菜（蓝）/ 退单（橙）/ 冲正（红）
- 退菜：可多选、必填原因、联动后厨撤单、可重复打印
- 退单：二次确认显示大字金额、支持部分退款、支持多次退款（累计不超实收）
- 冲正：只对未结账有效、已结账只能退单、**桌台要释放** ← 最容易漏
- 权限：整单退单超阈值需店长授权，授权要记录
- 权限不足时按钮置灰 + 说明原因，不要点了才报错

## 金额红线（必须全部有测试）
  退款 > 实收          → 拒绝
  累计退款 > 实收      → 拒绝
  退款为 0 或负数      → 拒绝
  退菜金额 > 该行金额  → 拒绝
  已全退后再退        → 拒绝
  取整显式 AwayFromZero，全程 decimal

## Domain 缺口（不要自己改，在报告里列）
RefundRecord / IRefundRepository / RefundType / 部分退款状态

## 铁律
✅ 只改 UI/Views/Refund/ UI/ViewModels/Refund/ UI/Services/Refund/ 及测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln *.csproj
命名空间用 Refund，安全

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-501: 结账后退款/退菜/冲正"
git push -u origin task/T-501-refund
```

---

# ② 给仓鼠2号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠2号。
Wave 5 你负责挂单/取单/预结账（T-502）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-502-hangorder
git pull origin main

## 任务卡
client/tasks/T-502_挂单_仓鼠2号.md

## 一句话
正餐服务员的核心动作之一是挂单，现在完全没有。客人说"等朋友"，
服务员只能干站着等。

## 重点
- **桌台"空闲"和"有挂单"必须是两个独立状态** ← 这是本卡最容易做错的点
  挂单后桌台要能开新单，但挂单信息不能丢
- 挂单列表要能快速取单（服务员高频操作，桌号要大而醒目）
- 超时挂单变色 + 批量清理有二次确认
- 取单时桌台被占要提示并允许换桌
- 预结账/拆单：能选拆哪几个菜（不是对半），拆出金额实时更新

## 边界（全部有测试）
  拆单选空            → 拒绝
  拆单金额 > 总额     → 拒绝
  拆完全部→ 订单 Settled
  拆单后剩负数        → 拒绝
  多次预结账累加正确
  取整显式 AwayFromZero，全程 decimal

## Domain 缺口（不要自己改，在报告里列）
HangOrder / IHangOrderRepository / ParkedOrder 状态 / 拆单关联

## 铁律
✅ 只改 UI/Views/HangOrder/ UI/ViewModels/HangOrder/ UI/Services/HangOrder/ 及测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln *.csproj
命名空间用 HangOrder，安全

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-502: 挂单/取单/预结账"
git push -u origin task/T-502-hangorder
```

---

# ③ 给仓鼠3号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠3号。
Wave 5 你负责系统设置与设备管理（T-503）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-503-settings
git pull origin main

## 任务卡
client/tasks/T-503_设置_仓鼠3号.md

## 背景
我们要 299 买断卖给小商家。商家装上软件后必须自己配好打印机、
门店信息、抹零规则才能用。配置做不好 = 老板直接骂娘。
**这是 299 定价能不能成立的前提。**

## 重点
### 打印机管理（本卡最重要）
- 能**自动发现**本机打印机
- 能**测试打印** ← 核心功能，商家靠它确认打印机调好了
- 显示在线/离线状态
- 一张单可配多台打印机；每个档口可指定后厨打印机
- Linux 设备路径（/dev/usb/lp*、/dev/ttyUSB*、/dev/ttyACM*、
  /dev/rfcomm*、TCP 9100）和 Windows 路径要分别处理

### 诊断报告（本卡最重要）
决定客服成本：
  没有它：商家描述问题 → 你远程连过去 → 每次 30 分钟
  有它：商家点一下 → 发文件给你 → 你 30 秒看完
- 包含：版本号、打印机状态、网络状态、数据库完整性、最近 7 天错误日志
- 数据库不存在/网络断开时不能崩

### 抹零规则
- 要有**预览示例**（输入 ¥38.5，按规则显示「抹到元→ ¥38.0」）
- 小票内容设置复用 T-107 的 RenderPreview，**不要重造**

### 其他
- logo 上传要能预览 + 自动压缩
- 一键备份 / 一键恢复（恢复前二次确认 + 先备份当前数据）
- 账号禁用后**历史订单操作人仍可查**（财务凭证不能丢）
- 同步状态显示 / 上次同步时间 / 可手动触发

## UI 要求
设置界面要「安静」：少动效、少装饰、高信息密度，不要转场动画。
状态用**颜色 + 文字双重要**（不要只靠颜色，色盲用户看不到）。
错误信息说人话：「打印机连接失败，检查线缆或换USB 口」，不是「PrintException」。

## Domain 缺口（不要自己改，在报告里列）
StoreSetting / PrinterConfig / SystemConfig 及各自仓储

## 铁律
✅ 只改 UI/Views/Settings/ UI/ViewModels/Settings/ UI/Services/Settings/ 及测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln *.csproj
命名空间用 Settings，安全

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-503: 系统设置与设备管理"
git push -u origin task/T-503-settings
```

---

# Lead 验收流程

```
1. 越界检查（只能改自己的独占目录）
2. 全量编译 net8.0 + net6.0
3. 全量测试（期望 547 + 新增全绿）
4. python tools/guardrails.py（16 项）
5. 代码质量抽查（不只看测试数）
6. 合并 --no-ff 到 main
```

**四个常见坑**：
```
CI 报红        → git fetch && git rebase origin/main
CS0118 撞名    → 别名要写在 namespace 之后，不是文件顶部
护栏报红       → 那是护栏在工作，别绕过，按提示改
测试没提交     → 说「完成」之前必须 commit + push
```

**验收时重点抽查**（这三张卡各有最易错的点）：
```
T-501 退款金额红线 + 冲正后桌台释放
T-502 桌台空闲与挂单状态独立性
T-503 诊断报告在异常环境下不崩
```