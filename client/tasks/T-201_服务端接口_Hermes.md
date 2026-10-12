# 任务卡 T-201 · 服务端 P0 接口补齐

> **收件人：Hermes** · 项目：逸华餐饮收银 · 工作区：`（略）

---

## 0. 项目背景

**逸华餐饮收银** —— 自有品牌餐饮 POS 系统，四端架构。

| 项 | 内容 |
|---|---|
| 服务端技术栈 | Python 3.12 + FastAPI + SQLAlchemy 2 + SQLite/MySQL |
| 客户端 | .NET 8/6 + Avalonia（另一组 AI 在做） |
| 部署 | Docker Compose（服务端 + MySQL + Nginx）/ systemd |

**服务端现状**：骨架已完成
```
✅ 18 张表 / 9 个业务域
✅ 47 项冒烟测试全通过
✅ Docker 部署套件齐备
⬜ 需补齐客户端要调的 P0 接口
```

---

## 1. 铁律

```
✅ 你只能修改：
      server/app/**
      server/tests/**
      server/seed.py / smoke_test.py

❌ 禁止修改：
      server/app/models.py 的表结构（如确需改，先提给 Lead）
      deploy/**  （部署配置由 Lead 维护）
      其他任何目录
```

---

## 2. 任务目标

客户端一期需要 **约 32 个接口**，补齐到「能完整跑一天营业」的程度。

---

## 3. 已有资产（**这是你的最大优势**）


```
├── calls.json           132 条完整调用记录
├── endpoints.json        52 个唯一接口
└── by_endpoint/          每个接口一份，含请求体+响应体
```

**不用猜字段名和结构，照着实现即可。**

---

## 4. 需实现的接口清单

### P0-1 登录 / 账号（5 个）

| 接口 | 说明 |
|---|---|
| `POST /saas/login` | 登录（**已实现**，需完善 `user_info.tradeType` 等字段） |
| `POST /saas/loginConfirm` | 多账号确认 |
| `GET /saas/refreshJwt` | 令牌续期 |
| `POST /saas/logout` | 登出 |
| `GET /store/getStoreConfig/` | 门店配置（**已实现**） |

### P0-2 商品同步（4 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/api/incr/productData` | 增量同步查询（**已实现**） |
| 商品 CSV 静态托管 | 13 张表的 CSV（**已实现**） |
| `POST /pos/operate/getOperatelist` | 操作权限列表（**已实现**） |
| 版本比对 | 各表最新版本查询 |

### P0-3 桌台（6 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/area/list` | 区域列表（**已实现**） |
| `POST /pos/desk/list` | 桌台列表（**已实现**） |
| `POST /pos/desk/openDesk` | 开台 |
| `POST /pos/desk/transferDesk` | 换台 |
| `POST /pos/desk/clearDesk` | 清台 |
| `POST /pos/area/saveStoreDesk` | 保存桌台 |

### P0-4 订单（8 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/order/submit` | 下单 |
| `POST /pos/order/settle` | 结账 |
| `POST /pos/order/detail` | 订单详情 |
| `POST /pos/order/cancel` | 撤单 |
| `POST /pos/order/refund` | 退单 |
| `POST /pos/order/reverseSettle` | 反结账 |
| `POST /pos/order/page` | 订单分页 |
| `POST /pos/order/preSettle` | 预结账 |

### P0-5 交班（3 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/shift/open` | 开班 |
| `POST /pos/shift/close` | 交班 |
| `POST /pos/handover/getHandoverList` | 交接记录 |

### P0-6 打印模板（3 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/print/sync/template` | 模板同步（**抓包里有 19.6KB 真实数据**） |
| `POST /pos/print/sync/device` | 设备同步 |
| `POST /pos/print/strategy/list` | 打印策略 |

### P0-7 门店配置（3 个）

| 接口 | 说明 |
|---|---|
| `POST /pos/store/info` | 门店信息（**已实现**） |
| `POST /open/store/queryAbilityOpen` | 功能开关 |
| `POST /open/merchant/queryMerchantConfig` | 商户配置 |

---

## 5. 关键技术要求

### 5.1 响应格式（**照抄真实契约，别自己设计**）

有两种格式，抓包里都有：

```json
// 格式一：ResponseBase（登录用）
{"code": "200", "msg": "成功", "data": {...}}

// 格式二：ApiResponse + 二次编码（业务接口用）
{"code": 200, "data": "{\"areaList\":[...]}", "success": true, "trace_no": "..."}
```

**注意**：业务接口的 `data` 是**二次编码的 JSON 字符串**，不是对象。

### 5.2 幂等（**核心**）

所有写接口必须支持 `clientOrderNo` 幂等键：

```
客户端断网 → 本地记账 → 联网后补传
如果服务端不去重 → 会产生重复订单
```

要求：
- `client_order_no` 建唯一索引
- 重复提交返回 `409` + **原单据完整信息**
- 客户端收到 409 视为成功

### 5.3 多租户隔离

所有查询强制带 `tenant_id` / `store_id` 过滤。

---

## 6. 验收标准

```
□ smoke_test.py 从 47 项扩展到 ≥ 80 项，全部通过
□ 幂等测试：同一 clientOrderNo 提交 10 次，数据库只有 1 条
□ 二次编码格式正确（用真实抓包的格式验证）
□ pytest 单元测试覆盖率 ≥ 70%
```

---

## 7. 提交要求

```
1. 改动文件清单
2. python smoke_test.py 输出（全部通过）
3. 新增接口的请求/响应示例
```

---

## 8. 参考

**接口契约规范**（52 个接口的字段名与类型，你的主要参考）：
```
client/docs/协议规范-服务端接口.md
```

**服务端现状**：
```
server/README.md      服务端说明
server/app/models.py  数据模型
server/smoke_test.py  现有测试
```

**注意**：契约文档里只有字段名和类型，具体业务值需要你自己设计合理的测试数据。

---


## 9. 完成后

反馈给 Lead，通过后进入 Wave 2：
```
你的下一个任务：T-202 幂等完善 + T-203 商品增量同步协议
```
