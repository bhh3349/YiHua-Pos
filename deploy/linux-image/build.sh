#!/usr/bin/env bash
#
# 逸华餐饮收银 —— Linux 镜像构建脚本
#
# 用法：
#   sudo ./build.sh --app <客户端发布目录> [选项]
#
# 必须在 Linux 上运行（debootstrap 是 Linux 工具）。
# Windows 开发机可用 WSL2。
#
set -euo pipefail

# ============================================================================
#  默认参数
# ============================================================================
ARCH="amd64"
APP_DIR=""
OUT_DIR="./out"
IMAGE_SIZE="8G"
DEBIAN_VER="bookworm"
ENABLE_SSH="no"
HOSTNAME="yihua-pos"
TIMEZONE="Asia/Shanghai"
LOCALE="zh_CN.UTF-8"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ============================================================================
#  参数解析
# ============================================================================
usage() {
    cat <<'EOF'
用法: sudo ./build.sh --app <目录> [选项]

必填:
  --app <dir>        客户端发布目录（dotnet publish 的输出）

选项:
  --arch <arch>      目标架构 amd64|arm64            默认 amd64
  --out <dir>        输出目录                        默认 ./out
  --size <size>      镜像总大小（如 8G）              默认 8G
  --debian <ver>     Debian 版本                     默认 bookworm
  --ssh              启用 SSH                        默认关闭
  --hostname <name>  主机名                          默认 yihua-pos
  -h, --help         显示帮助
EOF
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)     ARCH="$2"; shift 2 ;;
        --app)      APP_DIR="$2"; shift 2 ;;
        --out)      OUT_DIR="$2"; shift 2 ;;
        --size)     IMAGE_SIZE="$2"; shift 2 ;;
        --debian)   DEBIAN_VER="$2"; shift 2 ;;
        --ssh)      ENABLE_SSH="yes"; shift ;;
        --hostname) HOSTNAME="$2"; shift 2 ;;
        -h|--help)  usage ;;
        *) echo "未知参数: $1"; usage ;;
    esac
done

# ============================================================================
#  前置检查
# ============================================================================
log()  { echo -e "\033[1;34m[$(date +%H:%M:%S)]\033[0m $*"; }
warn() { echo -e "\033[1;33m[警告]\033[0m $*"; }
die()  { echo -e "\033[1;31m[错误]\033[0m $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "必须用 root 运行（需要 mount / chroot）"

for cmd in debootstrap parted mkfs.ext4 mkfs.vfat rsync chroot; do
    command -v "$cmd" >/dev/null 2>&1 || die "缺少工具: $cmd（先 apt install debootstrap parted dosfstools rsync）"
done

[[ -n "$APP_DIR" ]] || die "必须指定 --app <客户端发布目录>"
[[ -d "$APP_DIR" ]] || die "客户端发布目录不存在: $APP_DIR"
[[ -f "$APP_DIR/YiHua.Pos.dll" ]] || warn "$APP_DIR 里没找到 YiHua.Pos.dll，确认是发布输出？"

case "$ARCH" in
    amd64) DEB_ARCH="amd64" ;;
    arm64) DEB_ARCH="arm64" ;;
    *) die "不支持的架构: $ARCH" ;;
esac

# ============================================================================
#  准备
# ============================================================================
VERSION="$(date +%Y%m%d)"
IMG_NAME="yihua-pos-${VERSION}-${ARCH}.img"
IMG_PATH="${OUT_DIR}/${IMG_NAME}"
WORK_DIR="${OUT_DIR}/.work-${ARCH}"

mkdir -p "$OUT_DIR"
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR"

log "=========================================="
log " 逸华餐饮收银 —— Linux 镜像构建"
log "=========================================="
log " 架构     : $ARCH"
log " 客户端   : $APP_DIR"
log " 镜像     : $IMG_PATH ($IMAGE_SIZE)"
log " Debian   : $DEBIAN_VER"
log " SSH      : $ENABLE_SSH"
log "=========================================="

# ============================================================================
#  1. 创建镜像文件与分区
# ============================================================================
log "[1/9] 创建镜像文件并分区..."

truncate -s "$IMAGE_SIZE" "$IMG_PATH"

parted -s "$IMG_PATH" mklabel gpt
# BIOS 引导分区（GRUB 需要 1MB 的 bios_grub）
parted -s "$IMG_PATH" mkpart primary 1MiB 2MiB
parted -s "$IMG_PATH" set 1 bios_grub on
# /boot
parted -s "$IMG_PATH" mkpart primary fat32 2MiB 514MiB
# rootfs-A
parted -s "$IMG_PATH" mkpart primary ext4 514MiB 2562MiB
# rootfs-B
parted -s "$IMG_PATH" mkpart primary ext4 2562MiB 4610MiB
# data
parted -s "$IMG_PATH" mkpart primary ext4 4610MiB 100%

# 映射成块设备
LOOP="$(losetup --show -fP "$IMG_PATH")"
log "  循环设备: $LOOP"
trap 'cleanup' EXIT

cleanup() {
    set +e
    umount -R "$WORK_DIR/mnt" 2>/dev/null
    [[ -n "${LOOP:-}" ]] && losetup -d "$LOOP" 2>/dev/null
    set -e
}

mkfs.vfat -F32 -n BOOT "${LOOP}p2" >/dev/null
mkfs.ext4 -q -L rootfs-A "${LOOP}p3"
mkfs.ext4 -q -L rootfs-B "${LOOP}p4"
mkfs.ext4 -q -L data "${LOOP}p5"

# ============================================================================
#  2. debootstrap
# ============================================================================
log "[2/9] debootstrap 安装基础系统（这一步较慢，约 3-8 分钟）..."

mkdir -p "$WORK_DIR/mnt"
mount "${LOOP}p3" "$WORK_DIR/mnt"

debootstrap --arch="$DEB_ARCH" --variant=minbase \
    --include=systemd,systemd-sysv,dbus,ca-certificates,locales,tzdata \
    "$DEBIAN_VER" "$WORK_DIR/mnt" \
    "http://deb.debian.org/debian/"

# ============================================================================
#  3. chroot 配置
# ============================================================================
log "[3/9] 配置基础系统..."

mount --bind /dev  "$WORK_DIR/mnt/dev"
mount --bind /proc "$WORK_DIR/mnt/proc"
mount --bind /sys  "$WORK_DIR/mnt/sys"
mount -t devpts devpts "$WORK_DIR/mnt/dev/pts" 2>/dev/null || true

cat > "$WORK_DIR/mnt/etc/apt/sources.list" <<EOF
deb http://deb.debian.org/debian $DEBIAN_VER main contrib non-free-firmware
deb http://deb.debian.org/debian $DEBIAN_VER-updates main contrib non-free-firmware
deb http://security.debian.org/debian-security $DEBIAN_VER-security main contrib non-free-firmware
EOF

chroot "$WORK_DIR/mnt" /bin/bash -e <<EOF
export DEBIAN_FRONTEND=noninteractive

# 时区与语言
ln -sf /usr/share/zoneinfo/$TIMEZONE /etc/localtime
echo "$TIMEZONE" > /etc/timezone
sed -i "s/^# *$LOCALE/$LOCALE/" /etc/locale.gen
echo "LANG=$LOCALE" > /etc/default/locale
locale-gen

echo "$HOSTNAME" > /etc/hostname

apt-get update -qq
apt-get install -y --no-install-recommends \\
    linux-image-$DEB_ARCH grub-pc-bin grub-efi-$DEB_ARCH-bin \\
    firmware-linux-free \\
    xserver-xorg-core xserver-xorg-video-intel xserver-xorg-video-amdgpu \\
    xserver-xorg-input-libinput xserver-xorg-input-evdev \\
    xinit x11-xserver-utils \\
    fonts-noto-cjk fonts-noto-color-emoji \\
    alsa-utils \\
    sudo curl wget htop \\
    usbutils pciutils \\
    dosfstools e2fsprogs

# .NET 运行时（客户端是框架依赖模式）
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /opt/dotnet
rm -f /tmp/dotnet-install.sh
EOF

if [[ "$ENABLE_SSH" == "yes" ]]; then
    chroot "$WORK_DIR/mnt" apt-get install -y --no-install-recommends openssh-server
fi

# ============================================================================
#  4. 创建用户与目录
# ============================================================================
log "[4/9] 创建用户与数据目录..."

chroot "$WORK_DIR/mnt" /bin/bash -e <<'EOF'
# 收银专用用户组（外设权限靠它）
groupadd -f yihua

# 收银用户（无密码，自动登录）
id -u yihua &>/dev/null || useradd -m -g yihua -G video,audio,input,plugdev -s /bin/bash yihua
passwd -d yihua 2>/dev/null || true

# 应用目录
mkdir -p /opt/yihua/app /data/{config,logs,backup,upgrade}
chown -R yihua:yihua /opt/yihua /data
EOF

# ============================================================================
#  5. 安装客户端
# ============================================================================
log "[5/9] 安装客户端..."

rsync -a --delete "$APP_DIR/" "$WORK_DIR/mnt/opt/yihua/app/"
chroot "$WORK_DIR/mnt" chown -R yihua:yihua /opt/yihua/app

# ============================================================================
#  6. 应用 overlay（systemd / udev / 启动脚本）
# ============================================================================
log "[6/9] 应用配置覆盖层..."

rsync -a "$SCRIPT_DIR/overlay/" "$WORK_DIR/mnt/"
chmod +x "$WORK_DIR/mnt"/usr/local/bin/*.sh 2>/dev/null || true

# systemd 服务启用
chroot "$WORK_DIR/mnt" systemctl enable yihua-pos.service 2>/dev/null || true
chroot "$WORK_DIR/mnt" systemctl enable yihua-overlay.service 2>/dev/null || true
chroot "$WORK_DIR/mnt" systemctl set-default graphical.target

# ============================================================================
#  7. fstab
# ============================================================================
log "[7/9] 生成 fstab..."

BOOT_UUID="$(blkid -s UUID -o value "${LOOP}p2")"
DATA_UUID="$(blkid -s UUID -o value "${LOOP}p5")"

cat > "$WORK_DIR/mnt/etc/fstab" <<EOF
UUID=$BOOT_UUID  /boot  vfat  defaults,noatime  0 2
UUID=$DATA_UUID  /data  ext4  defaults,noatime  0 2
tmpfs            /tmp   tmpfs defaults,nosuid,nodev,size=256M  0 0
EOF

# ============================================================================
#  8. GRUB（A/B 双启动）
# ============================================================================
log "[8/9] 安装引导..."

mount "${LOOP}p2" "$WORK_DIR/mnt/boot" 2>/dev/null || {
    mkdir -p "$WORK_DIR/boot"
    mount "${LOOP}p2" "$WORK_DIR/boot"
    rsync -a "$WORK_DIR/mnt/boot/" "$WORK_DIR/boot/" 2>/dev/null || true
    umount "$WORK_DIR/mnt/boot" 2>/dev/null || true
    mount "${LOOP}p2" "$WORK_DIR/mnt/boot"
}

chroot "$WORK_DIR/mnt" grub-install --target=i386-pc --recheck "$LOOP" 2>/dev/null || \
    warn "BIOS 引导安装失败（如果是 UEFI-only 机器可忽略）"

# 生成 A/B 两套菜单项
ROOTFS_A_UUID="$(blkid -s UUID -o value "${LOOP}p3")"
ROOTFS_B_UUID="$(blkid -s UUID -o value "${LOOP}p4")"

cat > "$WORK_DIR/mnt/etc/grub.d/40_custom" <<EOF
#!/bin/sh
exec tail -n +3 \$0

menuentry "逸华收银 (系统 A)" --id yihua-a {
    search --no-floppy --fs-uuid --set=root $ROOTFS_A_UUID
    linux /boot/vmlinuz root=UUID=$ROOTFS_A_UUID ro quiet splash
    initrd /boot/initrd.img
}

menuentry "逸华收银 (系统 B - 备用)" --id yihua-b {
    search --no-floppy --fs-uuid --set=root $ROOTFS_B_UUID
    linux /boot/vmlinuz root=UUID=$ROOTFS_B_UUID ro quiet splash
    initrd /boot/initrd.img
}
EOF
chmod +x "$WORK_DIR/mnt/etc/grub.d/40_custom"

# 默认从 A 启动，超时 3 秒
sed -i 's/^GRUB_TIMEOUT=.*/GRUB_TIMEOUT=3/' "$WORK_DIR/mnt/etc/default/grub"
sed -i 's/^GRUB_DEFAULT=.*/GRUB_DEFAULT=yihua-a/' "$WORK_DIR/mnt/etc/default/grub"

chroot "$WORK_DIR/mnt" update-grub

# ============================================================================
#  9. 收尾
# ============================================================================
log "[9/9] 收尾..."

chroot "$WORK_DIR/mnt" /bin/bash -e <<'EOF'
apt-get clean
rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
EOF

# 卸载
umount -R "$WORK_DIR/mnt"
losetup -d "$LOOP"
LOOP=""

# ============================================================================
#  完成
# ============================================================================
log "=========================================="
log " ✅ 构建完成"
log "=========================================="
log " 镜像: $IMG_PATH"
log " 大小: $(du -h "$IMG_PATH" | cut -f1)"
log ""
log " 刷机: sudo ./installer/make-usb.sh --image $IMG_PATH --device /dev/sdX"
log " 或  : 用 Rufus / balenaEtcher 直接写盘"
log "=========================================="

sha256sum "$IMG_PATH" > "${IMG_PATH}.sha256"
log " 校验: ${IMG_PATH}.sha256"

rm -rf "$WORK_DIR"
