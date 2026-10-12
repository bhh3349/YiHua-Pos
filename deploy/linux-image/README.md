# Linux 收银镜像

> 逸华餐饮收银 —— Linux 触屏一体机交付形态（**主力**）

---

## 1. 为什么做镜像

| 项 | Windows 版 | **Linux 镜像** |
|---|---|---|
| 授权成本 | 需要 Windows 授权 | **0** |
| 系统维护 | 会中毒/弹广告/自动更新 | **只读根，不会坏** |
| 部署方式 | 装系统 + 装驱动 + 装软件 | **刷一次，开机即用** |
| 远程升级 | 逐台操作 | **A/B 分区，一键回滚** |
| 磁盘占用 | 20GB+ | **~2GB** |

**目标**：一体机出厂刷好镜像，商家开机就能收银。

---

## 2. 镜像设计

### 2.1 分区布局

```
┌──────────────────────────────────────────────────┐
│ 分区        文件系统   大小      用途              │
├──────────────────────────────────────────────────┤
│ /boot       FAT32      512 MB   引导 + 内核        │
│ rootfs-A    ext4       2 GB      系统 A（当前）    │
│ rootfs-B    ext4       2 GB      系统 B（备用/升级）│
│ data        ext4       剩余      业务数据          │
└──────────────────────────────────────────────────┘
```

**A/B 双系统**：升级时写入空闲分区 → 切换启动项 → 失败自动回滚。

### 2.2 系统特性

| 特性 | 实现 |
|---|---|
| 基础系统 | Debian 12 minimal（debootstrap） |
| 图形栈 | **Weston（Wayland）** 或最小 X11 |
| 桌面环境 | **无** —— 直接跑全屏收银应用 |
| 根文件系统 | **只读** + overlayfs（写操作重定向到 tmpfs） |
| 启动 | systemd → 自动登录 → 全屏应用 |
| 数据 | `/data` 分区持久化（SQLite + 配置 + 日志） |
| 外设权限 | 预置 udev 规则 + `yihua` 用户组 |
| 网络 | 有线优先，DHCP；可选 WiFi |
| 远程管理 | SSH（可选关闭）+ 自研升级服务 |

### 2.3 启动流程

```
BIOS/UEFI
  ↓
GRUB（选择 rootfs-A 或 rootfs-B）
  ↓
内核 + initramfs
  ↓
systemd
  ├── 挂载只读 rootfs
  ├── 挂载 /data
  ├── overlayfs 合并（可写层 → tmpfs）
  ├── 加载 udev 规则（外设权限）
  ├── 启动图形栈（Weston）
  └── 启动 yihua-pos.service
        └── 全屏收银应用（目标 ≤ 20 秒）
```

---

## 3. 目录结构

```
deploy/linux-image/
├── README.md              本文档
├── build.sh               构建脚本（在 Linux 上运行）
├── config/
│   ├── packages.list      要安装的包
│   ├── grub.cfg           引导配置（A/B 切换）
│   └── fstab.template     分区挂载模板
├── overlay/               覆盖到系统里的文件
│   ├── etc/
│   │   ├── systemd/system/yihua-pos.service
│   │   ├── systemd/system/yihua-overlay.service
│   │   ├── udev/rules.d/99-yihua-pos.rules
│   │   └── weston/weston.ini
│   └── usr/local/bin/
│       ├── yihua-start.sh       启动脚本
│       └── yihua-upgrade.sh     升级脚本
├── scripts/
│   ├── make-rootfs.sh     构建根文件系统
│   ├── make-image.sh      打包成 .img
│   └── verify-image.sh    校验镜像
└── installer/
    └── make-usb.sh        制作刷机 U 盘
```

---

## 4. 构建步骤

> ⚠️ **构建必须在 Linux 上运行**（debootstrap 是 Linux 工具）。
> Windows 开发机可以用 WSL2 或一台 Linux 虚拟机。

```bash
# 依赖
sudo apt install -y debootstrap qemu-user-static parted kpartx

# 构建（需要 root）
sudo ./build.sh --arch amd64 --app ./publish/linux --out ./out

# 产物
out/
├── yihua-pos-1.0.0-amd64.img      镜像文件（可写盘）
├── yihua-pos-1.0.0-amd64.img.sha256
└── yihua-pos-1.0.0-amd64.manifest  软件清单
```

### 4.1 参数

| 参数 | 说明 | 默认 |
|---|---|---|
| `--arch` | 目标架构 `amd64` / `arm64` | `amd64` |
| `--app` | 客户端发布目录（`dotnet publish` 的输出） | 必填 |
| `--out` | 输出目录 | `./out` |
| `--size` | 镜像总大小 | `8G` |
| `--data-size` | data 分区大小 | `剩余全部` |
| `--with-ssh` | 启用 SSH | 关闭 |
| `--debian` | Debian 版本 | `bookworm` |

### 4.2 客户端发布（构建镜像前）

```bash
# 在 Windows 开发机上
dotnet publish client/src/YiHua.Pos.App/YiHua.Pos.App.csproj \
  -c Release -f net8.0 -r linux-x64 --self-contained false \
  -o publish/linux
```

**框架依赖模式**（22.7 MB）—— .NET 运行时由镜像提供。

---

## 5. 刷机

### 5.1 U 盘制作

```bash
# 在 Linux 上
sudo ./installer/make-usb.sh --image out/yihua-pos-1.0.0-amd64.img --device /dev/sdX
```

**Windows 上**：用 [Rufus](https://rufus.ie/) 或 `balenaEtcher` 直接写 `.img`。

### 5.2 安装到目标机

```
1. U 盘插入一体机
2. BIOS 设置 U 盘启动（关闭 Secure Boot）
3. 开机自动进入安装界面
4. 选择目标硬盘 → 自动分区 → 写入
5. 拔 U 盘，重启
6. 进入收银界面
```

---

## 6. 升级机制

### 6.1 A/B 分区升级

```bash
# 在设备上（或远程执行）
sudo yihua-upgrade.sh --image /data/upgrade/yihua-pos-1.1.0-amd64.img
```

流程：
```
1. 校验新镜像（SHA256 + 签名）
2. 确定当前启动分区（A 或 B）
3. 写入空闲分区
4. 切换 GRUB 默认启动项
5. 重启
6. 新系统启动成功 → 标记为「正常」
   启动失败（3 次）→ 自动回滚到旧分区
```

### 6.2 回滚

```bash
sudo yihua-upgrade.sh --rollback
```

---

## 7. 外设权限（udev）

Linux 下普通用户默认**无权访问** USB/串口设备。镜像预置规则：

```udev
# /etc/udev/rules.d/99-yihua-pos.rules

# 打印机（USB 打印类）
SUBSYSTEM=="usb", ATTRS{bInterfaceClass}=="07", MODE="0666", GROUP="yihua"

# USB 转串口芯片（收银外设最常用）
KERNEL=="ttyUSB*", MODE="0666", GROUP="yihua"
KERNEL=="ttyACM*", MODE="0666", GROUP="yihua"

# 板载串口（电子秤/客显）
KERNEL=="ttyS[0-9]*", MODE="0666", GROUP="yihua"

# 并口（老式打印机）
KERNEL=="lp[0-9]*", MODE="0666", GROUP="yihua"

# USB 打印机设备节点
SUBSYSTEM=="usbmisc", KERNEL=="usb/lp*", MODE="0666", GROUP="yihua"
```

**用户组**：`yihua` 组，收银应用以该组用户运行。

---

## 8. 数据持久化

| 路径 | 内容 | 分区 |
|---|---|---|
| `/data/yihua.db` | SQLite 数据库（订单/商品/桌台） | data |
| `/data/config/` | 应用配置 | data |
| `/data/logs/` | 运行日志 | data |
| `/data/upgrade/` | 待安装的升级包 | data |
| `/data/backup/` | 自动备份 | data |

**根文件系统只读**，所有写操作都在 `/data`。

---

## 9. 目标硬件

| 项 | 要求 |
|---|---|
| 架构 | x86_64（ARM64 备选） |
| CPU | Intel/AMD，2 核以上 |
| 内存 | ≥ 2 GB（推荐 4 GB） |
| 存储 | ≥ 8 GB（SSD 或 DOM） |
| 显卡 | Intel/AMD 集显（内核 i915 / amdgpu 驱动） |
| 触摸屏 | USB HID 或 I2C（内核 hid-multitouch） |
| 网卡 | Realtek RTL8111 / Intel（内核 r8169 / e1000e） |

**这些都是 Linux 内核自带驱动的型号**，开箱即用。

---

## 10. 已知限制

| 限制 | 说明 |
|---|---|
| 刷脸支付设备 | 只有 Windows SDK，Linux 无法支持 |
| 部分封闭一体机 | BIOS/UEFI 对 Linux 不友好，需实测 |
| 定制 I2C 触摸屏 | 个别型号需额外驱动 |

---

## 11. 构建环境准备（WSL2 方案）

Windows 开发机可以用 WSL2 构建：

```powershell
# 安装 WSL2 + Ubuntu
wsl --install -d Ubuntu-22.04

# 进入 WSL 后
sudo apt update
sudo apt install -y debootstrap qemu-user-static parted kpartx dosfstools

# 把仓库挂载进来（Windows 盘符自动挂载在 /mnt/）
cd /mnt/c/WorkSpace/purge/_public/deploy/linux-image
sudo ./build.sh --arch amd64 --app /mnt/c/WorkSpace/purge/_public/publish/linux --out ./out
```
