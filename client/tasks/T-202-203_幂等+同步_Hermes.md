# 任务卡 T-202 + T-203 · 幂等加固 + 商品增量同步

> **收件人：Hermes** · 分支：`task/T-202-203-sync`

---

## 0. 环境说明（**这次写清楚了，不会再绕路**）

| 项 | 值 |
|---|---|
| **唯一协作仓库** | `https://github.com/bhh3349/YiHua-Pos`（公开） |
| **你上次用的旧仓库** | `bhh3349/SAAS` —— **已废弃，只读归档，不要再推** |
| **基线** | `main` 最新提交（当前 `8564612`） |
| **你的分支** | `task/T-202-203-sync` |
| **推送目标** | `git push -u origin task/T-202-203-sync`（`origin` 已指向公开仓库） |

```bash
cd C:\WorkSpace\purge\yihua-pos
git fetch origin
git checkout -b task/T-202-203-sync origin/main
git log --oneline -1        # 确认基线是 8564612 或更新
```

> **你上次遇到的谱系分叉问题已解决**：
> `origin` 现在是公开仓库，与 main 同源。
> 旧的 `archive` 远程（SAAS）保持只读，不要推。
> 如果本地还停在旧谱系，直接从 `origin/main` 开新分支即可。

---

## 1. 你上次的成果（已合并进 main）

| 项 | 结果 |
|---|---|
| T-201 服务端 P0 接口 | ✅ 已合并（`32203d9`） |
| 冒烟测试 | 47 → **157 项**全过 |
| pytest | **145 项**（1 跳过），覆盖率 89% |
| 你上报的复合唯一索引 | ✅ **已采纳并修复**（`8564612`） |
| 你发现的 `_recalc_discount` 缺 `flush()` | ✅ 保留你的修复 |

**你上报的 schema 建议是对的**，我已改成：
```python
UniqueConstraint("store_id", "client_order_no", name="uk_store_client_no")
```
测试也改了：原来那个测试记录的是错误行为，现在改为验证「跨门店可各建一单」+「同门店仍幂等」。

---

# 第一部分：T-202 加固

## 2. 目标

把上次遗留的三类问题收拾干净。

## 3. 任务清单

### 3.1 JWT 密钥强度（**CI 日志里有警告**）

```
InsecureKeyLengthWarning: The HMAC key is 11 bytes long,
which is below the minimum recommended length of 32 bytes for SHA256.
```

**要求**：
- 默认密钥长度 ≥ 32 字节
- 支持从环境变量读取（`DSH_POS_JWT_SECRET`）
- 启动时校验：密钥过短则**拒绝启动**（生产环境）或**打醒目警告**（开发环境）
- 开发环境自动生成的密钥要有明确提示（"这是开发密钥，生产必须替换"）

### 3.2 复合唯一约束的迁移脚本

我已经改了 `models.py` 的约束，但 **SQLite 改约束需要重建表**。

**要求**：
- 提供迁移脚本（`server/migrations/`）
- 检测旧库（有 `uk_client_no`）→ 重建 `t_order` 表 → 迁移数据 → 建新约束
- 幂等：重复执行不出错
- 有测试验证迁移前后数据一致

### 3.3 边界情况补齐

| 场景 | 期望行为 |
|---|---|
| `clientOrderNo` 为空字符串 | 回 400，不能落单 |
| `clientOrderNo` 超长（>64 字符） | 回 400 |
| 金额为负数 | 回 400 |
| 数量为 0 或负数 | 回 400 |
| 商品 ID 不存在 | 回 404 + 明确提示 |
| 桌台已被占用时开台 | 回 409 + 当前占用信息 |
| 同一订单并发结账（两次请求同时到） | 只有一次成功，另一次回 409 |
| 超长备注（>255 字符） | 截断或回 400 |

**并发结账**这条最重要——用数据库事务 + 行锁保证。

---

# 第二部分：T-203 商品增量同步

## 4. 目标

客户端**只拉变化的数据**，不是每次都全量下载。

## 5. 现状

`/api/v1/catalog/syncUrl` 目前返回**所有表**的 CSV 地址，客户端每次都要全下。

## 6. 设计要求

### 6.1 版本比对

客户端第一次同步后记住各表的版本号，之后只拉变化的表。

```
POST /api/v1/catalog/version
→ {
    "versions": {
      "Product": 12, "ProductCategory": 3, "ProductUnit": 1,
      "ProductSpec": 5, "ProductAttr": 2, ...
    },
    "serverTime": "2026-10-12 21:30:00"
  }
```

### 6.2 按需拉取

```
POST /api/v1/catalog/syncUrl
Body: { "knownVersions": { "Product": 12, "ProductCategory": 3 } }
→ {
    "urls": [
      { "table": "ProductSpec", "version": 5, "url": ".../ProductSpec_5.csv" }
      // 只返回版本变化的表；Product 版本没变则不返回
    ],
    "unchanged": ["Product", "ProductCategory"],
    "ossBase": "http://..."
  }
```

### 6.3 版本号来源

- 每张表有 `version` 列（已存在）
- 表的「当前版本」= 该表所有行的 `max(version)`
- 商品增删改时 `version` 递增

### 6.4 CSV 文件命名

```
{table}_{version}.csv       例：Product_12.csv
```

文件名带版本号 → 客户端可以缓存，浏览器/CDN 也能缓存。

### 6.5 兼容性

- 不传 `knownVersions` → 返回全部表（向后兼容）
- 传了 → 只返回变化的

---

## 7. 验收标准

```
□ 冒烟测试从 157 项扩展到 ≥ 190 项，全部通过
□ pytest 从 145 项扩展到 ≥ 175 项，全部通过，覆盖率 ≥ 85%
□ 新增测试覆盖：
    T-202:
      - JWT 密钥过短时启动行为正确
      - 迁移脚本：旧库 → 新库，数据一致
      - 8 类边界情况各有用例
      - 并发结账：只有一次成功
    T-203:
      - 首次同步（无 knownVersions）返回全部表
      - 二次同步（版本未变）返回空 urls
      - 商品改一个 → 只有 Product 表版本变化
      - CSV 文件名带版本号
□ CI 全绿（客户端 + 服务端 + 越界检查）
```

---

## 8. 铁律

```
✅ 你只能修改：
      server/**

❌ 禁止修改：
      client/**      （客户端由其他 AI 负责）
      deploy/**      （部署配置由 Lead 负责）
      docs/**        （文档由 Lead 负责）
      README.md

❌ 不直接推 main
```

> 注：`server/app/models.py` 上次你按规矩没动、上报给 Lead —— 这次如果还需要改，
> **同样先报**。但 T-202 的迁移脚本本身是新增文件，可以直接写。

---

## 9. 完成后

```bash
cd server
python seed.py --reset
python run.py --port 8000     # 另开一个终端
python smoke_test.py
python -m pytest tests/ -v

git fetch origin && git rebase origin/main
git add -A
git commit -m "T-202+T-203: 幂等加固 + 商品增量同步"
git push -u origin task/T-202-203-sync
```

告诉 Lead：「T-202 + T-203 完成，分支 task/T-202-203-sync」
附：
1. 改动文件清单
2. smoke_test + pytest 输出（含覆盖率）
3. 增量同步的请求/响应示例
4. **如果又发现架构级问题，照上次那样上报，不要擅自改**
