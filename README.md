# 逸华餐饮收银

自有品牌餐饮 POS 系统，四端架构：**Linux 收银镜像（主力） · Windows 收银端 · 安卓平板端 · 管家 App · 自建服务端**

---

## 仓库位置

| 项 | 值 |
|---|---|
| **远程仓库** | `https://github.com/bhh3349/SAAS`（私有） |
| **本地路径** | `C:\WorkSpace\purge\yihua-pos` |
| **主分支** | `main` |

---

## 目录结构

```
yihua-pos/
├── client/                    客户端（.NET 8/6 + Avalonia）
│   ├── YiHua.Pos.sln
│   ├── src/
│   │   ├── YiHua.Pos.App/         主程序
│   │   ├── YiHua.Pos.UI/          控件与设计 Token
│   │   ├── YiHua.Pos.Domain/      领域模型 + 接口  🔒 冻结
│   │   ├── YiHua.Pos.Data/        本地 SQLite
│   │   ├── YiHua.Pos.Hardware/    硬件层（打印/电子秤/钱箱）
│   │   ├── YiHua.Pos.Services/    业务服务
│   │   └── YiHua.Pos.Modules.*/   业务模块（一模块一项目）
│   └── tasks/                 任务卡（发给各 AI）
├── server/                    服务端（Python + FastAPI）
├── deploy/                    部署（Docker / systemd / Linux 镜像）
├── docs/                      文档
├── tools/                     工具（品牌替换 / 契约抓取 / 重建验证）
├── patch/                     客户端补丁脚本
├── assets/                    品牌素材
```

---

## 多 AI 协作规则（**必读**）

### 三条铁律

| 原则 | 说明 |
|---|---|
| **① 目录独占** | 一个任务 = 一个目录 = 一个 Owner，任何时刻一个目录只有一个 AI 在写 |
| **② 一模块一项目** | 每个业务模块独立 `.csproj`，连项目文件都不撞 |
| **③ 公共文件冻结** | 只有 Lead 能改（见下表），需要改时提「接口变更请求」 |

### 🔒 冻结文件（Worker 只能读，不能写）

```
client/YiHua.Pos.sln
client/Directory.Build.props
client/src/YiHua.Pos.Domain/**
client/src/YiHua.Pos.Hardware/Abstractions/**
client/src/YiHua.Pos.UI/Themes/Tokens.axaml
client/src/YiHua.Pos.App/App.axaml.cs
```

### 目录所有权

| 目录 | Owner |
|---|---|
| `client/src/YiHua.Pos.Domain/**` | 🔒 Lead |
| `client/src/YiHua.Pos.Hardware/Printing/EscPos/**` | 仓鼠1号 |
| `client/src/YiHua.Pos.Hardware/Scales/**` | 仓鼠2号 |
| `client/src/YiHua.Pos.Data/**` | 仓鼠3号 |
| `client/src/YiHua.Pos.UI/Controls/**` | 仓鼠3号 |
| `client/src/YiHua.Pos.Modules.Login/**` | 仓鼠2号 |
| `client/src/YiHua.Pos.Modules.Product/**` | 仓鼠3号 |
| `client/src/YiHua.Pos.Modules.OrderMeal/**` | 仓鼠1号 |
| `client/src/YiHua.Pos.Modules.Desk/**` | Hermes |
| `server/**` | Hermes |
| `deploy/**` | 🔒 Lead |

---

## 环境准备

### 客户端

```powershell
# 需要 .NET 8 SDK（本机已装在 work\dotnet）
dotnet build client/YiHua.Pos.sln          # 双目标编译（net8.0 + net6.0）
dotnet test  client/YiHua.Pos.sln
```

### 服务端

```bash
cd server
pip install -r requirements.txt
python seed.py --reset
python run.py --port 8000
python smoke_test.py
```

---

## 当前进度

| Wave | 内容 | 状态 |
|---|---|---|
| **Wave 0** | 项目骨架 + 冻结基线 | ✅ 完成 |
| **Wave 1** | ESC/POS · 电子秤协议 · 数据层 · 服务端接口 | ⬜ 进行中 |
| Wave 2 | Linux 打印 · 控件库 · 幂等同步 | ⬜ |
| Wave 3 | 打印模板 · 登录 · 商品 · 桌台 | ⬜ |
| Wave 4 | 点单 · 支付 · 订单 · 交班 | ⬜ |
| Wave 5 | 离线队列 · 快餐 · 报表 · 设置 | ⬜ |
| Wave 6 | Linux 镜像 · 体积优化 · Windows 安装包 | ⬜ |

---

## 文档

| 文档 | 内容 |
|---|---|
| [08-需求规格说明书](docs/08-需求规格说明书.md) | 功能清单、非功能需求、验收标准 |
| [09-技术方案](docs/09-技术方案.md) | 技术栈、架构、外设方案、离线方案 |
| [10-开发任务分解](docs/10-开发任务分解.md) | 34 个任务卡片 |
| [11-多AI协作规范](docs/11-多AI协作规范.md) | 防撞车机制、波次计划 |
| [协议规范 · 电子秤](client/docs/协议规范-电子秤.md) | 3 个品牌的串口协议（帧格式/指令/解析） |
| [协议规范 · 服务端接口](client/docs/协议规范-服务端接口.md) | 52 个接口的字段名与类型 |

---

## ⚠️ 合规红线

- 本项目为**独立实现**，只借鉴公开的行业规范与协议标准（如 ESC/POS 指令集、串口秤协议）
- 不得引入任何第三方代码、资源或素材
- 不要往仓库里提交 token / 密钥 / 生产数据
