# Wave 6 启动提示词

> Wave 5 已完成：退款 / 挂单 / 设置 / 团购服务端已合并，**692 测试全绿**。
> 当前 main：`21b952b`
> 复制对应段落发给对应的人。

---

## 通用信息

| 项 | 值 |
|---|---|
| **唯一仓库** | `https://github.com/bhh3349/YiHua-Pos.git` |
| **基线** | `main` 最新（`21b952b`，692 个测试） |
| **本地路径（Hermes）** | `C:\WorkSpace\purge\yihua-pos` |

## Wave 6 分工

| 谁 | 任务 | 分支 | 独占目录 |
|---|---|---|---|
| **仓鼠1号** | T-601 团购验券与核销 | `task/T-601-groupbuy-verify` | `GroupBuy` |
| **仓鼠2号** | T-602 沽清管理 | `task/T-602-soldout` | `SoldOut` |
| **仓鼠3号** | T-603 会员与营销 | `task/T-603-member` | `Member` |

**12 个独占目录，零重叠，可完全并行。**

> **Hermes 本波不派活** —— 服务端团购已就绪，客户端三家做完后再定。

---

## 本波的产品判断（**很重要，别跑偏**）

用户明确说过：**单店小店用不上进销存**。

所以：

```
✅ 沽清（今天这道菜卖完了）      → 要做，小店天天用
❌ 进销存（入库/盘点/报损/采购）→ 不做，已从任务卡删除
```

**做之前先问「单店会不会用」，不要按大连锁的想象加功能。**

---

## Wave 6 的三条铁律

### ① 别重复定义已有的东西

Wave 5 Domain 刚补齐，**这些字段/枚举已存在，直接用**：

```
Order.GroupBuyPlatform / GroupBuyCouponCode / AmountGroupBuy
Product.MemberPrice / Product.IsSoldOut
PayType.Member
CashRule.AllowMemberDiscount
RefundRecord / HangOrderRecord（已在 Domain，不要再定义一份）
```

### ② 命名空间撞名（Wave 3 真实踩过）

CS0118 的解法是**把别名写在 `namespace` 语句之后**，不是文件顶部：

```csharp
namespace YiHua.Pos.UI.Services.Member;

// 放这里
using SomeType = YiHua.Pos.Domain.Entities.SomeType;
```

### ③ Domain 冻结

本波缺口在报告里列，**不要自己改**。

> ⚠️ 另外：`V001_Init.sql` 是**手写 SQL 建表**，
> 任何 Domain 字段变更必须同步 `Migrations/*.sql`，
> 否则会打挂所有 Data 层测试（金额一律 TEXT 存，REAL 会丢精度）。

---

# ① 给仓鼠1号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠1号。
Wave 6 你负责团购券记账（T-601）。**这一波优先级最高。**

git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-601-groupbuy-verify
git pull origin main

任务卡：client/tasks/T-601_团购验券_仓鼠1号.md

## 这张卡只做三件事
1. 验券    客人在美团 App 出示二维码，收银员扫码 → 验出券信息和抵扣额
2. 记券    平台 + 券码 + 抵扣额，记到订单上
3. 抵扣进优惠   AmountDiscount，不是收款

## ⚠️ 券是怎么来的（别搞错）
客人在美团/饿了么 App 点开自己的券 → App 显示二维码
→ 收银员用扫码枪扫这个二维码 → 系统拿到券码 → 调服务端验券

**平台代金券不是实体券，没有纸券。**
收银员不需要也不应该手输券码。手工输入只作为客人二维码加载不出来的兜底。

扫码枪在收银场景通常表现为「键盘输入 + 回车」，
所以输入框要 autofocus，支持扫码枪前缀后缀过滤
（部分扫码枪用 F9 触发、回车结尾），防止把收银员敲的键也当券码。

## ⚠️ 金额口径（最重要）
团购券抵扣算优惠，不算收款：

  订单原价 100
  团购券抵扣 30      → 计入 AmountDiscount（优惠）
  实收（现金/微信）  70

❌ 不能记成收款「团购 70 元」——平台结的是核销金额 30，不是实付 70。

现有代码 OrderEntryViewModel：
  AmountDiscount = LineDiscountTotal + OrderDiscount + WipeZeroAmount
要加第四项 GroupBuyDiscount。
⚠️ 那一行在 ViewModels/Ordering/ 属于别人的目录，你不要改，
   把计算放 Services/GroupBuy/，报告里说明，我合并时接上。

## 服务端接口（已定稿）
POST /api/v1/groupbuy/coupon/verify
     { platformCode, couponCode, storeId }

⚠️ 双层封装：data 字段里再套一层 JSON 字符串，必须反序列化两次。
   写 DoubleEnvelopeTests 验证。

返回两个金额：
  faceValue       面值（满 30 券，面值 30）
  discountAmount  本单实际抵扣
用 discountAmount，界面同时显示面值。平台按实际核销金额结账，用面值对不上账。

## 8 种券状态错误码，每种给人话
  COUPON_USED           → 这张券已经用过了，让客人出示新的券
  COUPON_EXPIRED         → 券已过期，请客人确认有效期
  COUPON_STORE_MISMATCH  → 这张券只能在指定门店使用
  COUPON_TIME_RESTRICTED → 这张券只能在特定时段用（如仅限午市）
  COUPON_NEED_BOOKING    → 这张券需要提前预约
  COUPON_NOT_FOUND       → 券码不存在，请核对后重试
  COUPON_NOT_YET_VALID   → 券还没到使用时间
  PLATFORM_MISMATCH      → 这张券不属于该平台

理由：收银员看到「券无效」没法处理客人，
     看到「只能在午市用」才知道该跟客人怎么说。

## ⚠️ 断网怎么办（用系统已有的优惠，不是新功能）
断网验不了，但抵扣额是确定的（客人 App 上券面写着）：
  收银员按券面金额直接打折 —— 就是优惠金额/抹零，本来就有
  券信息（平台 + 券码）照样记，月底对账用

❌ 不做「手填团购抵扣额」——那不是真实优惠，凭空多一个收款方式，
   报表显示「团购收入 30」永远跟平台对不上。

提示要说清：「券验证需要联网。可以按券面金额直接打折。」不卡住收银。

代金券在 T-603 会员模块，本卡不重复造，接口留好即可。

## 已有东西直接用
Order.GroupBuyPlatform / GroupBuyCouponCode / AmountGroupBuy 已存在，不要重复定义。

## 铁律
✅ 只改 UI/Views/GroupBuy/ UI/ViewModels/GroupBuy/ UI/Services/GroupBuy/ 及测试
❌ 不碰 Domain/ Data/ Controls/ Themes/ Ordering/ Product/ sln *.csproj

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-601: 团购券记账"
git push -u origin task/T-601-groupbuy-verify

报告要含：优惠口径怎么接到 Ordering（我合并时要改哪一行）、
扫码枪前缀后缀的处理方式、8 种错误码映射表、双层JSON解包方式
```

---
# ② 给仓鼠2号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠2号。
Wave 6 你负责沽清管理（T-602）。

git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-602-soldout
git pull origin main

任务卡：client/tasks/T-602_沽清_仓鼠2号.md

## ⚠️ 第一件事：这不是进销存
用户明确说过「单店不需要进销存」，我已经把进销存从任务卡删了。

✅ 要做：沽清（今天这道菜卖完了）—— 小店天天用
❌ 不做：入库/出库/盘点/报损/损耗统计/批次/保质期/采购单/供应商/对账

30 平米的苍蝇馆子，老板早上数一下牛肉够不够，
晚上直接扔剩菜。他不会打开电脑录「今日报损 3 斤，损耗 ¥78」。

## 为什么值得做
11:40 牛肉面卖完了，出餐口贴张纸
12:30 服务员还在下单，收银台直接出单 → 客人等 10 分钟被告知没有

线上更糟：外卖平台上「牛肉面」还在卖，客人付款，
店里说卖完了 → 差评 + 平台扣分。

## 核心功能
1. 手动沽清：沽清后点单时该商品置灰 + 「已沽清」
   （不能下单成功才报错）
2. 批量沽清：按档口一键沽清（比一个个点快十倍）、按关键字批量
3. 定时自动还原：默认次日 09:00，不用老板手动清
4. 多时段沽清：午市菜 14:00 自动沽清晚餐菜
5. 可打印（贴在出餐口）

## ⚠️ 手工沽清 vs 定时沽清必须分开
  老板手工沽清「今天这道菜没有」 → 明天 09:00 自动恢复
  定时沽清「这道菜只在午市」     → 定时恢复

如果用一个 IsSoldOut 布尔搞定，会出现
「老板手工沽清的菜第二天没人记得为什么不见了」。
数据模型上就要分开。

## 外卖平台同步（接口预留，本期不做真对接）
定义 ISoldOutSync 接口，self_platform 实现，
美团/饿了么/抖音只留骨架返回 NotSupported。

**业务代码不得出现 if platform == "meituan"** ← 与 T-405 同一条铁律，
写 NoPlatformBranchTests（AST 扫描）保证。

理由：你 99 元/平台/年卖的就是这个。现在不接真对接，
但架构上不能堵死。

## 定时逻辑必须注入时钟
不要写死 DateTime.Now，否则永远测不到。
参考 InMemoryTicketQueue 的做法。

## UI
- 沽清列表按档口分组（老板按档口操作，不是按拼音）
- 已沽清用**灰色 + 售罄标签**，不要用鲜红色
  （客人还看得见菜单，红色像报错）
- 大按钮（后厨手上有油，小屏幕触控）
- 批量确认要显示明确范围：「将沽清热菜档 8 道菜」
- 定时设置说人话：「每天 09:00 恢复供应」，不是「Reset Time: 09:00」

## 已有东西直接用
Product.IsSoldOut 已存在，直接用，不要重复定义。

## 铁律
✅ 只改 UI/Views/SoldOut/ UI/ViewModels/SoldOut/ UI/Services/SoldOut/ 及测试
❌ 不碰 Domain/ Data/ Controls/ Themes/ Ordering/ Product/ sln *.csproj

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-602: 沽清管理"
git push -u origin task/T-602-soldout

报告要含：手工 vs 定时沽清的数据模型区别、定时还原实现、平台同步接口签名
```

---

# ③ 给仓鼠3号

```
你是「逸华餐饮收银」项目的客户端开发工程师，代号 仓鼠3号。
Wave 6 你负责会员与营销（T-603）。

git clone https://github.com/bhh3349/YiHua-Pos.git
cd YiHua-Pos
git checkout -b task/T-603-member
git pull origin main

任务卡：client/tasks/T-603_会员_仓鼠3号.md

## 背景
稍大一点的店要做活动：会员价、会员折扣、充值送余额、菜品券折扣券。
会员是 299 之外的增值收入。

## ⚠️ 先读任务卡第 0 节：合规
《单用途商业预付卡管理办法（试行）》（2012 年商务部令第 9 号，2016 修订，
目前仍现行有效）适用于餐饮业发卡企业。

备案责任在商家，不在你的软件。但这几条会直接约束你的设计：

  第十八条  单张记名卡限额 ≤ 5000 元，不记名卡 ≤ 1000 元
            → 充值金额上限要做校验，默认 5000
  第十九条  记名卡不得设有效期
            → 会员不能有「有效期」字段
  第二十一条 退卡须留存姓名/证件号/卡号/金额；余额 < 100 元可付现金
            → 要有退卡流程和身份留存
  第十七条 记名卡购卡需实名并留存身份信息 5 年
            → 开卡手机号必填（小店最现实的实名方式）

法规原文：https://www.gov.cn/zhengce/2012-09/21/content_5712346.htm
地方规定可能更严（如上海有专门实施办法），所以软件里要放一个「合规提示」页。

## 明确不做
❌ 充值资金存管（商家银行账户的事）
❌ 在线开卡（实体店扫码已够）
❌ **「充值送」「充1000送100」这类赠送活动 —— 本期完全不做**

商家要送钱，直接用「余额调整」功能，填个原因就行，不用搞赠送活动。

## 余额调整才是高频功能（比充值还常用）
  客人投诉了 → 老板改余额 +10，填「客人投诉补偿」
  员工点错了 → 老板改余额 -5，填「点单点错」

要求：
  - 可加可减
  - **必须填原因**，原因用选择列表（赠送/补偿/纠错/其他），不让打字
  - 每次记流水：金额、原因、操作人、时间
  - 调整后余额不能为负
  - 能查某会员全部余额流水（为什么少了钱要能查清）

## 会员价（最高频）
- Product.MemberPrice 已有，结账自动切换
- **必须明确显示「会员价 ¥18（原价 ¥22）」**，不能让客人自己发现
- 折扣优先级：单品会员价 > 会员折扣 > 普通折扣 > 抹零
  （顺序错了老板算不清账）
- 会员折扣与券**互斥不能叠加**
- 会员价高于普通价 → 拒绝启用（配置错误）

## 优惠券（比充值更实用）
券类型：满减 / 折扣 / 代金 / 兑换
- 满减要有门槛（满 100 减 20）
- 券核销必须幂等，同一张券不能重复用
- 有有效期，过期自动失效

⚠️ 本平台的团购券（T-601）在服务端已有完整实现，
本卡的「优惠券」是商家自发的营销券，两回事，不要混。

## 边界（全部要测）
  充值 5001 → 拒绝；充值 5000 → 允许
  充1000送100 → 实收 1000 与赠送 100 分开记账
  余额不能为负；余额支付有可打印凭证
  退卡余额 < 100 可现金；≥ 100 需记录退款方式
  同一张券重复核销 → 只成功一次
  会员折扣 + 券 → 互斥
  9.95 × 0.9 → 8.96（AwayFromZero，不是 8.955→8.95）
  停用会员 → 历史消费记录保留（财务凭证不能丢）
  手机号重复开卡 → 拒绝
  手机号脱敏显示 138****8000

## 已有东西直接用
Product.MemberPrice / PayType.Member / CashRule.AllowMemberDiscount 已存在，
RefundRecord / HangOrderRecord 已在 Domain，不要重复定义。

## 铁律
✅ 只改 UI/Views/Member/ UI/ViewModels/Member/ UI/Services/Member/ 及测试
❌ 不碰 Domain/ Data/ Controls/ Themes/ Ordering/ sln *.csproj

## 完成后
dotnet build + dotnet test + python tools/guardrails.py
git fetch origin && git rebase origin/main
git commit -m "T-603: 会员与营销"
git push -u origin task/T-603-member

报告要含：折扣优先级决策树、余额调整记账方式、合规校验清单、Domain 缺口
```

---

# Lead 验收流程

```
1. 越界检查（只能改自己的独占目录）
2. 全量编译 net8.0 + net6.0
3. 全量测试（期望 692 + 新增全绿）
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

**本波验收重点抽查**：
```
T-601 抵扣额 > 订单应付要拒绝；断网时待同步券在界面可见
T-602 手工沽清与定时沽清在数据模型上分开了；业务代码无平台分支
T-603 充值上限 5000 拒绝 5001；赠送金额与充值金额分开记账
```