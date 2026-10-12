# 任务卡 T-405 · 团购平台对接（服务端）

> **收件人：Hermes** · 分支：`task/T-405-groupbuy`

---

## 0. 环境

| 项 | 值 |
|---|---|
| **仓库** | `https://github.com/bhh3349/YiHua-Pos` |
| **本地路径** | `C:\WorkSpace\purge\yihua-pos` |
| **基线** | `main` 最新（当前 `2cab19f`） |
| **分支** | `task/T-405-groupbuy` |

```bash
cd C:\WorkSpace\purge\yihua-pos
git fetch origin
git checkout -b task/T-405-groupbuy origin/main
```

> ⚠️ **只有你能改 `server/**`。** 其他人在做客户端，不要碰客户端目录。

---

## 1. 你独占的目录

```
✅ 只能碰：
    server/app/routers/groupbuy.py
    server/app/routers/webhook.py          （如需）
    server/app/services/groupbuy/*.py
    server/app/models_groupbuy.py          （如需；models.py 冻结）
    server/tests/test_groupbuy*.py

❌ 冻结（不要动）：
    server/app/models.py                   ← 现有实体，其他模块在用
    server/app/db.py                       ← 现有建表逻辑
    server/app/main.py                     ← 只有在需要注册路由时才碰，改动最小
    任何 client/ 目录
```

> 如果确实需要改 `models.py`，**先在报告里说明**并把改动单独提一个 commit，我会评估。

---

## 2. 业务背景：团购到底是什么

**餐饮团购的三种形态**（主流平台都这三种）：

```
① 团购券（核销型）
   平台卖「¥99 双人套餐」，客人买了得到券码，到店出示核销。
   → 商户这边要做的是「验券核销」

② 到店付（支付型）
   平台卖「¥9.9 抵 20 元代金券」，客人在平台付款，到店直接消费。
   → 商户要「核销」并记录抵用金额

③ 配送单（外卖型）
   客人从平台点外卖，平台推单给商户，商户做出来，平台配送。
   → 商户要「接单/出餐/回传状态」
```

**本期重点做 ① ②（核销）**，③ 只做接口预留。

---

## 3. 真实平台的共性

**没有一家平台的接口是一样的**，但业务模型一致：

```
核销：
  平台 → 商户：这是一张券（券码、金额、有效期、限制条件）
  客人到店 → 商户输券码 → 验证 → 核销 → 生成订单
  
验证要点：
  - 券码是否存在
  - 是否已核销（不能重复用）
  - 是否在有效期内
  - 是否满足限制（限门店/限时段/限桌/仅限新客）
  - 核销金额（代金券抵多少）
```

---

## 4. 功能要求

### 4.1 平台配置（多平台）

**一台收银机可能同时接美团/饿了么/抖音/微信/自有小程序**。

```
platform_code: meituan | eleme | douyin | wechat | self
门店绑定：每个平台各绑一个门店 ID
密钥：sign_key（平台签名用）
```

**要求**：
- 平台配置**加密存储**（密钥不能明文落库/落日志）
- 支持一个平台绑多个门店
- 配置界面（API）要能增删改查
- **平台 code 用枚举/常量**，不要散落字符串

### 4.2 券的验证

```
POST /api/v1/groupbuy/coupon/verify
{
  "platformCode": "meituan",
  "couponCode": "1234567890",
  "storeId": "200001"
}
```

**返回**：
```
{
  "valid": true,
  "coupon": {
    "couponCode": "...",
    "platformCode": "meituan",
    "platformCouponId": "...",
    "couponName": "双人套餐",
    "couponType": "package" | "cash",
    "faceValue": 99.00,          // 面额
    "discountAmount": 99.00,     // 抵扣金额
    "validFrom": "...",
    "validTo": "...",
    "restrictions": {...}        // 使用限制
  }
}
```

**必须校验（每一项都要有测试）**：

| 校验 | 失败返回 |
|---|---|
| 券码不存在 | `COUPON_NOT_FOUND` |
| 已核销 | `COUPON_USED` |
| 未到有效期 | `COUPON_NOT_YET_VALID` |
| 已过期 | `COUPON_EXPIRED` |
| 门店不匹配 | `COUPON_STORE_MISMATCH` |
| 时段不符（如仅限午市） | `COUPON_TIME_RESTRICTED` |
| 需要预约但没预约 | `COUPON_NEED_BOOKING` |
| 平台不匹配 | `PLATFORM_MISMATCH` |

**要求**：错误要**区分清楚**，不能都返回「券无效」。

### 4.3 核销

```
POST /api/v1/groupbuy/coupon/redeem
{
  "platformCode": "meituan",
  "couponCode": "...",
  "orderId": 12345,          // 关联的本地订单
  "actualDiscount": 99.00,   // 实际抵扣（可能商家让利部分）
  "operatorId": 1
}
```

**要求**：
- **幂等**：同一张券只能核销一次（这是资金问题，必须严格）
- 核销后要**锁定券状态**
- 核销记录要能查（谁、什么时候、哪个订单、抵了多少）
- 支持**撤销核销**（误操作），但要记录撤销人和原因

### 4.4 与本地订单联动

**关键**：核销要能生成/关联本地订单。

```
方式 A：先建订单（正常点单）→ 结账时选「用团购券」→ 抵扣 → 核销
方式 B：先验券 → 直接按券内容建订单（客人就是来吃套餐的）
```

**推荐 A**（灵活），但 B 也要支持。

- 订单要记录**用了哪个平台的哪张券**
- 订单金额要区分：**原价 / 团购抵扣 / 实付**
  ```
  原价     ¥128.00
  团购抵扣 -¥99.00
  实付      ¥29.00
  ```
- 报表要能分出团购收入（T-310 会用到，**上报这个需求**）

### 4.5 对账（**重要但常被忽略**）

```
平台说今天在你店核销了 50 张券，抵了 ¥4,950
我们系统记录的是 48 张，¥4,720
差异：2 张，¥230
```

**要求**：
- 提供**对账接口**：按日/按平台拉取核销记录
- 提供**差异对比**功能
- 核销记录要能**导出**（CSV，UTF-8 BOM，Excel 打开中文不乱码）

### 4.6 平台对接适配层（**架构关键**）

**不要在业务代码里写死美团的逻辑**。

```
groupbuy/
├── services/
│   ├── platform.py          # PlatformAdapter 抽象基类
│   ├── meituan.py           # 美团适配
│   ├── eleme.py             # 饿了么适配
│   ├── douyin.py            # 抖音适配
│   └── self_platform.py     # 自有小程序适配
└── routers/
    └── groupbuy.py          # 业务路由，调 adapter，不含平台细节
```

**抽象接口**：
```python
class PlatformAdapter(ABC):
    def verify_coupon(self, req) -> CouponVerifyResult: ...
    def redeem_coupon(self, req) -> RedeemResult: ...
    def reverse_redeem(self, req) -> bool: ...
    def reconcile(self, date) -> list[PlatformRedeemRecord]: ...
    def verify_sign(self, payload, sign) -> bool: ...   # 签名校验
```

**要求**：
- 业务路由**只依赖 `PlatformAdapter`**，不出现 `if platform == "meituan"`
- 加新平台 = 加一个 adapter 文件，**不改业务代码**（这是硬要求）
- 本期只实现 `self_platform`（自有）完整逻辑，其余**留骨架**（返回「平台未接入」）

### 4.7 签名与安全

- 平台回调要**验签**（防伪造）
- 券码要**校验格式**（长度、字符集），防注入
- 所有金额用 `Decimal`，**禁止 float**
- 敏感信息（密钥）**日志里脱敏**
- 接口要**限流**（防刷券码）

---

## 5. ⚠️ Domain 缺口 / 需协调

**需要客户端配合的**（**在报告里列出来，我分配给客户端的人**）：

| 缺口 | 需要谁做 |
|---|---|
| 订单表要有 `groupbuyPlatform` / `groupbuyCouponCode` / `groupbuyDeductAmount` 字段 | 客户端 Domain（**冻结，我来加**） |
| 报表要分团购收入 | 客户端 T-310 已在上报 |
| 客户端要有「验券/核销」界面 | **后续 Wave 5 任务** |
| 结账页要能选「用团购券」 | **后续 Wave 5 任务** |

**本期你只做服务端**。客户端界面我另开任务。

---

## 6. 测试要求（**必须覆盖**）

```
□ 每个校验项都有测试（8 种券状态）
□ 幂等：同一券核销两次，第二次必须失败
□ 撤销核销后可重新核销
□ 平台路由：走 adapter，不含平台硬编码分支
□ new adapter 加入不需改业务代码（用测试证明）
□ 签名校验：正确签名通过、错误签名拒绝
□ 金额精度：全程 Decimal，无 float
□ 并发核销：同一券并发两个请求，只能成功一个
□ 对账：差异能算出来
□ CSV 导出：UTF-8 BOM
□ 空券码/超长券码/特殊字符
```

---

## 7. 交付物

```
server/app/routers/groupbuy.py
server/app/services/groupbuy/
├── __init__.py
├── platform.py             PlatformAdapter 抽象 + 注册表
├── self_platform.py        自有平台完整实现
├── meituan.py              骨架（返回「平台未接入」）
├── eleme.py                骨架
├── douyin.py               骨架
├── reconciliation.py       对账逻辑
└── errors.py               错误码定义（细分，不用笼统的「券无效」）

server/app/models_groupbuy.py    团购相关实体（不污染现有 models.py）
server/tests/
├── test_groupbuy_verify.py      8 种券状态验证
├── test_groupbuy_redeem.py      核销 + 幂等 + 撤销
├── test_groupbuy_concurrent.py  并发核销
├── test_groupbuy_platform.py    adapter 路由（加新平台不改业务）
├── test_groupbuy_sign.py        签名校验
└── test_groupbuy_reconcile.py   对账 + CSV 导出
```

> 记得更新 `server/app/main.py` 注册路由（**最小改动**）。

---

## 8. 验收标准

```
□ python -m pytest server/tests/ 全通过
□ python server/smoke_test.py 全通过（且可重复运行）
□ 8 种券状态错误码细分，不笼统
□ 核销幂等：并发下同一券只成功一次
□ 加新平台只加 adapter 文件，不改业务代码（用测试证明）
□ 金额全程 Decimal
□ 密钥不落日志
□ 签名校验有效
□ 对账能算差异
□ CSV 导出 UTF-8 BOM
□ 只改 server/**，未动 models.py（若动了，报告里说明）
□ 报告里列出客户端需要配合的字段与界面需求
```

---

## 9. 完成后

```bash
python -m pytest server/tests/ -q
python server/smoke_test.py

git fetch origin && git rebase origin/main
git add -A
git commit -m "T-405: 团购平台对接（适配层 + 券核销 + 对账）"
git push -u origin task/T-405-groupbuy
```

> ⚠️ **务必 commit 再报完成**（T-202/203 那次改动全在工作区没提交，差点白干）。

报告：
1. 改动清单 + 测试输出
2. **adapter 架构说明**（加新平台要改哪些文件）
3. 券状态与错误码对照表
4. **客户端需要配合的字段与界面清单**
5. 对账流程说明