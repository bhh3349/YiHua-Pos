# Wave 3 启动提示词

> Wave 2 已完成并合并：T-103 / T-106+T-107 / T-005。
> 当前 main：**225 个测试全绿**（net8.0 + net6.0）。
> 复制对应段落发给对应的仓鼠。

---

## 通用信息

| 项 | 值 |
|---|---|
| **唯一仓库** | `https://github.com/bhh3349/YiHua-Pos`（公开） |
| **基线** | `main` 最新，**开工前必须 `git pull origin main`** |
| **本地路径（Hermes 用）** | `C:\WorkSpace\purge\yihua-pos` |

## Wave 3 分工

| 仓鼠 | 任务 | 分支 | 独占目录 |
|---|---|---|---|
| **仓鼠1号** | T-301 登录与权限 | `task/T-301-login` | `UI/Views/Login/`, `UI/ViewModels/Login/` |
| | T-306 桌台管理 | `task/T-306-desk` | `UI/Views/Desk/`, `UI/ViewModels/Desk/` |
| | T-308 订单管理 | `task/T-308-order-manage` | `UI/Views/OrderManage/`, `UI/ViewModels/OrderManage/` |
| **仓鼠2号** | T-303 正餐点单 | `task/T-303-order-dinein` | `UI/Views/Ordering/`, `UI/ViewModels/Ordering/`, `UI/Services/Ordering/` |
| **仓鼠3号** | T-302 商品管理 | `task/T-302-product` | `UI/Views/Product/`, `UI/ViewModels/Product/` |
| | T-307 收银结账 | `task/T-307-payment` | `UI/Views/Payment/`, `UI/ViewModels/Payment/`, `UI/Services/Payment/` |
| **Hermes** | T-202+T-203 服务端 | `task/T-202-203-sync` | `server/**` |

---

# ① 给仓鼠1号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠1号。
Wave 3 你有**三个任务**，目录互不重叠，可依次做。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git pull origin main
git log --oneline -1        # 确认基线

## 任务卡（每个任务一个分支，做完一个再开下一个）
1. client/tasks/T-301_登录_仓鼠1号.md      → 分支 task/T-301-login
2. client/tasks/T-306_桌台_仓鼠1号.md      → 分支 task/T-306-desk
3. client/tasks/T-308_订单管理_仓鼠1号.md  → 分支 task/T-308-order-manage

## 铁律
✅ 只改任务卡里列出的独占目录
❌ 不碰 UI/Controls/、Themes/、Domain/、Data/、App/、sln、Directory.Build.props
❌ 不引入第三方 UI 库
❌ 不直接推 main

## 重要
任务卡里有几处「Domain 表达不了」的需求（规格组/冲正单/记账客户等）——
**不要自己改 Domain（冻结）**，在报告里列「Domain 缺口清单」，我统一处理。
这样能避免多方同时改冲突。

## 完成后
每个任务单独推分支，附：改动清单 + 测试输出 + 缺口清单
```

---

# ② 给仓鼠2号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠2号。
Wave 3 你负责正餐点单（T-303）。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git pull origin main

## 任务卡
client/tasks/T-303_正餐点单_仓鼠2号.md
分支：task/T-303-order-dinein

## 一句话概括
做正餐（火锅/中餐）的点单界面：加菜/规格/称重/备注/挂单/退菜/提交下单。
左商品区 + 右明细区，收银一体机横屏布局。

## 特别强调
**金额计算是整个 POS 最容易出错的地方**。
OrderCalculator 是纯逻辑类，要重点测：
- 称重（重量×单价，2位）
- 行折扣 / 整单折扣 / 抹零（可开关）
- 取整规则显式用 MidpointRounding.AwayFromZero（不要依赖默认值）
- 赠菜金额0但计入原价
- 退菜从合计扣除

提交下单要防重复（连点10次只产生1单），用 clientOrderNo 幂等键。

## 铁律
✅ 只改 UI/Views/Ordering/ UI/ViewModels/Ordering/ UI/Services/Ordering/ 及对应测试
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln
❌ 不要自己改 Domain（冻结）——缺口写进报告

## 完成后
推分支 task/T-303-order-dinein，附：
1. 改动清单 + 测试输出
2. Domain 缺口清单
3. 金额计算规则说明（含取整/抹零）
```

---

# ③ 给仓鼠3号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠3号。
Wave 3 你有两个任务：商品管理（T-302）+ 收银结账（T-307）。
目录不重叠，可依次做。

## 开工前
git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git pull origin main

## 任务卡
1. client/tasks/T-302_商品_仓鼠3号.md   → 分支 task/T-302-product
2. client/tasks/T-307_收银结账_仓鼠3号.md → 分支 task/T-307-payment

## T-302 商品管理要点
餐饮商品结构和商超不同，重点是：
- 多规格（大小/温度/糖度/加料）→ 规格组要能表达茶饮那种多维度
- 别名（客人说「牛肉面」，系统叫「兰州牛肉面(小)」）
- 必须有单位（份/例/杯/两/斤/串）
- 称重商品
- 套餐（售价不等于单品相加）
- 沽清（可逆的高频操作）
- 时价

## T-307 收银结账要点
**现在不做线上支付，接口预留**。要实现：
- 现金（算找零、开钱箱）
- 记账（挂账到客户）
- 自定义收款（客人扫商家自己的码，系统只记录，不找零）
- 微信/支付宝：只做 UI 入口 + 接口骨架，点击显示「暂未启用」

折扣/抹零仅店长，取整显式用 AwayFromZero。
打印失败不能回滚结账。
本单已被其他设备结账 → conflict 状态要有明确提示。

## 铁律
✅ 只改任务卡里列出的独占目录
❌ 不碰 Controls/ Themes/ Domain/ Data/ App/ sln
❌ 不要自己改 Domain（冻结）——缺口写进报告

## 完成后
每个任务单独推分支，附：改动清单 + 测试输出 + Domain 缺口清单 + 规则说明
```

---

# ④ 给 Hermes（如果还没交 T-202/203）

```
唯一协作仓库：https://github.com/bhh3349/YiHua-Pos（公开）
旧的 bhh3349/SAAS 已废弃，只读归档，不要再推。

cd C:\WorkSpace\purge\yihua-pos
git fetch origin
git checkout -b task/T-202-203-sync origin/main

任务卡：client/tasks/T-202-203_幂等+同步_Hermes.md

铁律：只能改 server/**；不直接推 main；
架构级问题照旧上报，不要擅自改。
```

---

# Lead 验收流程

分支推上来后我会做：
```
1. tools/integrate.py <分支>   越界检查 / 冻结文件 / 依赖白名单
2. 全量编译（net8.0 + net6.0）
3. 全量测试
4. 代码质量抽查（不只看测试数，会看实际实现）
5. 通过 → 合并 main；不通过 → 打回附具体错误
```

**注意**：Wave 2 有一个教训——
分支基于旧 main 时会缺少已有修复。
我会在验收时帮你 rebase 到最新 main，但如果 CI 报失败，
先 `git fetch origin && git rebase origin/main` 再看。