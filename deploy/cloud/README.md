# 服务端云端部署指南

> 服务端本身就是一个标准 Web 服务：**FastAPI + uvicorn/gunicorn，纯 HTTP + JSON**，
> 没有桌面依赖，可部署在任何 Linux/Windows 云主机、容器或 K8s 上。
> 本文覆盖：Docker 一键部署、裸机 systemd 部署、客户端指向云端、以及几个必须注意的坑。

---

## 1. 部署形态选择

| 形态 | 适用 | 数据库 | 命令 |
|---|---|---|---|
| **Docker Compose**（推荐） | 云主机、单店/多店 | MySQL 8 | `docker compose up -d` |
| **裸机 systemd** | 已有服务器、不想装 Docker | MySQL 8 | 见 `pos-server.service` |
| **单机一体化** | 收银机本机 | SQLite | `启动收银.exe`（已交付） |

三者跑的是**同一份代码**，只是数据库和进程管理方式不同。

---

## 2. Docker 一键部署（云服务器）

```bash
# 1) 上传 pos/ 目录到服务器
scp -r pos/ user@your-server:/opt/

# 2) 配置
cd /opt/pos/deploy/cloud
cp .env.example .env
vim .env            # 改 MySQL 密码、JWT 密钥、CLIENT_API_BASE

# 3) 起服务（首次会自动建表 + 灌演示数据 + 导出商品 CSV）
docker compose up -d

# 4) 验证
curl http://127.0.0.1/health
curl -X POST http://127.0.0.1/saas/login -H 'Content-Type: application/json' -d '{"userName":"admin"}'
```

容器拓扑：

```
Internet ──> nginx :443/:80 ──> pos-server :8000 ──> mysql :3306
                                     └──> /data 卷（商品 CSV 静态资源）
```

**首次启动自动完成**：建表 → 灌演示数据（admin/123456）→ 导出客户端兼容格式的商品 CSV。

---

## 3. 裸机 systemd 部署

```bash
# 依赖
apt install -y python3.12 python3.12-venv mysql-server nginx

# 代码 + 虚拟环境
mkdir -p /opt/pos-server && cd /opt/pos-server
# 上传 app/ run.py requirements.txt
python3.12 -m venv .venv
.venv/bin/pip install -r requirements.txt

# 数据库
mysql -e "CREATE DATABASE pos CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -e "CREATE USER 'pos'@'127.0.0.1' IDENTIFIED BY '你的密码';"
mysql -e "GRANT ALL ON pos.* TO 'pos'@'127.0.0.1';"

# 数据目录
mkdir -p /var/lib/pos-server && chown pos:pos /var/lib/pos-server

# 服务
cp pos-server.service /etc/systemd/system/
vim /etc/systemd/system/pos-server.service   # 改密码/JWT/域名
systemctl daemon-reload && systemctl enable --now pos-server
systemctl status pos-server
```

---

## 4. 环境变量

| 变量 | 说明 | 默认 |
|---|---|---|
| `DSH_POS_DB` | 数据库连接串 | `sqlite:///<data>/pos.db` |
| `DSH_POS_DATA` | 数据目录（SQLite + CSV） | exe 同级 `data/` |
| `DSH_POS_JWT_SECRET` | **生产必须改** | `dev-secret-change-me` |
| `DSH_POS_OSS_BASE` | 客户端拉 CSV 的地址前缀 | `http://127.0.0.1:8000/oss/` |
| `DSH_POS_TOKEN_TTL_MIN` | 登录令牌有效期（分钟） | 720 |

MySQL 连接串示例：

```
mysql+pymysql://pos:密码@mysql:3306/pos?charset=utf8mb4
```

> 代码里主键用 `BigInteger().with_variant(Integer, "sqlite")`，
> SQLite 自增和 MySQL `BIGINT AUTO_INCREMENT` 都能正常工作。

---

## 5. 客户端指向云端

改 Windows 客户端的 `appsettings.json`（放在 exe 同目录），把 15 个键全部指向云端：

```json
{
  "IsDebug": "1",
  "login": "https://pos.yourdomain.com",
  "api_url": "https://pos.yourdomain.com",
  "new_api_url": "https://pos.yourdomain.com",
  "new_api_url_core": "https://pos.yourdomain.com",
  "Base_New_Api_Url": "https://pos.yourdomain.com",
  "Statistics_Url": "https://pos.yourdomain.com",
  "Pay": "https://pos.yourdomain.com",
  "Member": "https://pos.yourdomain.com",
  "MarketUrl": "https://pos.yourdomain.com",
  "cashierManageUrl": "https://pos.yourdomain.com",
  "AdvertisingAddress": "https://pos.yourdomain.com",
  "VersionCheckAddress": "https://pos.yourdomain.com",
  "GoodsOSSAddress": "https://pos.yourdomain.com/oss/",
  "Register_Member_Url": "https://pos.yourdomain.com/register?store_code=",
  "OemCode": "",
  "IOT-Test": "0"
}
```

`pos/windows/appsettings.cloud.json` 是现成模板。

### 已实测

本机以 `0.0.0.0:8000` 启动，客户端指向 **局域网 IP**（等价于云端可达性）：

```
POST /saas/login                        -> 200
POST /pos/api/incr/productData          -> 200
GET  /oss/.../cyProduct.csv             -> 200
本地库：cy_product 12 行、cy_product_category 5 行、
        cy_product_store_relation 12 行、cy_product_category_relation 12 行
界面：登录成功 -> 收银主界面（12 个商品、6 个分类页签）
```

---

## 6. ⚠️ HTTPDNS 的坑（用域名时必看）

客户端 `HttpDnsService` 在请求**非 IP 主机名**时，会先把域名发给**阿里云 HTTPDNS**
（客户端内置了一个属于原发行方的账号），失败后才回落到系统 DNS。

```
SendHttpDnsRequestAsync():
    if (!UrlHelper.IsIpAddress(originalHost)) {
        var ips = await HttpDnsService.ResolveAsync(originalHost);   // ← 域名在这里被发出去
        ...
    }
    return await CreateApiClient().SendAsync(request);
```

| 客户端填的地址 | 是否触发 HTTPDNS | 说明 |
|---|---|---|
| `http://1.2.3.4`（IP） | ❌ 跳过 | 最干净，但没有 TLS |
| `https://pos.yourdomain.com` | ✅ 会查询一次 | 功能正常，但域名会出现在厂商账号的 DNS 查询里 |

**三种处理方式**：

1. **用 IP + 自签/无 TLS**（内网或测试环境够用）
2. **加 hosts 记录**（`/etc/hosts` 或 Windows hosts）把域名指向你的服务器 —— 仍然会先查 HTTPDNS，只是失败后回落更快
3. **补丁掉 `HttpDnsService.ResolveAsync`**，让它直接走系统 DNS（推荐上生产时做）

---

## 7. 上线检查清单

- [ ] `DSH_POS_JWT_SECRET` 改成足够长的随机串
- [ ] MySQL 密码不是默认值，且只监听内网
- [ ] `curl http://127.0.0.1:8000/docs` 只在内网可访问（或用 nginx 屏蔽 `/docs`）
- [ ] TLS 证书就位（`certs/fullchain.pem` + `privkey.pem`）
- [ ] 防火墙只放 80/443，**不要**把 8000/3306 暴露到公网
- [ ] 已应用「断网补丁」（`patch_no_phone_home.ps1`），客户端不再往厂商企业微信 webhook 发日志
- [ ] 数据备份：`mysqldump` 定时任务 + `/data` 卷快照
- [ ] 日志：`docker logs` 或 `journalctl`，并配置轮转
- [ ] 多 worker 时注意 `seed_if_empty()` 的并发（首次启动建议先单 worker 跑一次）

---

## 8. 目录说明

```
pos/deploy/cloud/
├── Dockerfile           服务端镜像（gunicorn + uvicorn worker）
├── docker-compose.yml   服务端 + MySQL + Nginx
├── nginx.conf           反向代理 / TLS / 静态资源
├── pos-server.service   systemd 单元（裸机部署）
├── .env.example         环境变量模板
└── README.md            本文件
```

## 9. 容量参考

| 规模 | 配置 | 说明 |
|---|---|---|
| 单店（1~5 台 POS） | 2C4G，SQLite 或 MySQL | 单机一体化即可 |
| 连锁（10~50 店） | 4C8G，MySQL 8，4 worker | Compose 单机 |
| 区域（100~500 店） | 8C16G + 读写分离，8~16 worker | 建议 K8s + 对象存储替代 `/oss` 本地卷 |
| 多租户 SaaS | 网关 + 服务拆分 + Redis 缓存 | 当前代码已按 `tenant_id` 隔离，可平滑扩展 |
