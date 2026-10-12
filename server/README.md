# 自有餐饮 POS 服务端（M1 骨架）

FastAPI + SQLAlchemy + SQLite（可一键切 MySQL）实现的后端，含**新契约**与**参考系统客户端兼容层**。

## 快速开始

```powershell
cd pos\server
pip install -r requirements.txt

python seed.py --reset      # 建库 + 演示数据（租户/门店/商品/桌台/会员）
python run.py --port 8000   # 启动

python smoke_test.py        # 端到端冒烟测试（47 项）
python export_legacy_csv.py # 导出参考系统格式商品 CSV（供 Windows 客户端拉取）
```

| 入口 | 地址 |
|---|---|
| 新契约 | `POST http://127.0.0.1:8000/api/v1/{domain}/{action}` |
| 兼容层 | `POST http://127.0.0.1:8000/saas/login`（参考系统客户端直连） |
| 静态资源 | `http://127.0.0.1:8000/oss/` |
| 交互文档 | `http://127.0.0.1:8000/docs` |

默认账号：`admin / 123456`

## 目录

```
server/
├── app/
│   ├── config.py      配置（DB / JWT / OSS）
│   ├── db.py          引擎与会话
│   ├── models.py      18 张表（SQLAlchemy）
│   ├── security.py    bcrypt + JWT
│   ├── util.py        响应封装 / 上下文 / CSV
│   ├── main.py        应用装配
│   └── routers/
│       ├── auth.py    登录 / 续期 / 资料
│       ├── catalog.py 商品 + CSV 同步通道
│       ├── desk.py    区域 / 桌台 / 开台 / 换台 / 并台 / 清台
│       ├── order.py   下单 / 改单 / 折扣 / 预结 / 结账 / 退单 / 反结账
│       ├── pay.py     支付方式 / 支付单 / 现金确认
│       ├── shift.py   开班 / 交班 / 交接记录
│       ├── member.py  会员 / 储值 / 积分
│       ├── report.py  汇总 / 趋势 / 支付方式 / 商品排行
│       └── legacy.py  参考系统客户端兼容层
├── seed.py            演示数据
├── smoke_test.py      端到端测试
├── export_legacy_csv.py
└── run.py
```

## 关键设计

**1. 统一响应**
```json
{ "code": "200", "message": "ok", "data": {...},
  "traceNo": "...", "timestamp": 1712345678901 }
```
`401` 未登录 / `409` 幂等冲突或业务规则拒绝（如余额不足、重复结账）。

**2. 幂等**
`order/create` 以 `clientOrderNo` 为唯一键；重复提交返回 `409` + 原单据，
客户端收到 409 即视为同步成功 —— 这是离线队列能安全重试的基础。

**3. 鉴权与多租户**
`Authorization: Bearer <jwt>`，JWT 内嵌 `tid`(租户) / `sid`(门店) / `term`(终端)；
所有查询强制带 `tenant_id` / `store_id` 过滤，杜绝跨租户越权。

**4. 商品同步**
- 新契约：`catalog/version` 查版本 → `catalog/syncUrl` 取 CSV 地址 → 客户端下载导入
- 兼容层：`export_legacy_csv.py` 按参考系统实体列名导出，客户端走 `/oss/.../cyproduct/*.csv`

**5. 切换 MySQL**
```powershell
$env:DSH_POS_DB="mysql+pymysql://user:pwd@127.0.0.1:3306/pos?charset=utf8mb4"
pip install pymysql
```
模型层用 `BigInteger().with_variant(Integer,"sqlite")` 处理主键，两边都能自增。

## 冒烟测试覆盖（47 项）

健康检查 · 登录成功/密码错误/无 token · 商品分类/列表/搜索/版本/CSV 导出与下载 ·
区域/桌台/开台 · 下单金额计算 · **幂等重复提交** · 折扣 · 预结 ·
**支付金额不足拒绝** · 现金结账与找零 · 结账后自动清台 · **重复结账拒绝** ·
会员余额支付与扣减 · 充值送额 · 开班/当前班次/交班/应有现金核对 ·
报表汇总/支付方式/商品排行 · 兼容层登录与增量同步与兜底

```
通过 47 项，失败 0 项
```

## 尚未实现（后续里程碑）

| 项 | 归属 |
|---|---|
| 扫码/聚合支付通道、退款到渠道 | M3 |
| MQTT 实时通道（云打印/叫号/多端同步） | M3 |
| 团购券核销、营销活动、优惠券 | M3 |
| 库存进出与预警 | M3 |
| 发票（数电票） | M6 |
| 报表导出 Excel/PDF | M3 |
| 多租户 SaaS 化（网关、限流、审计） | M6 |
