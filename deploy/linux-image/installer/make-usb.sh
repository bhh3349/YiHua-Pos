#!/usr/bin/env bash
#
# 逸华餐饮收银 —— U 盘刷机工具
#
# 用法：
#   sudo ./make-usb.sh --image out/yihua-pos-xxx.img --device /dev/sdX
#
# 会做三件事：
#   1. 校验镜像 SHA256
#   2. 二次确认目标设备（防止写错盘）
#   3. 写入并校验
#
set -euo pipefail

IMAGE=""
DEVICE=""
SKIP_CONFIRM="no"

log()  { echo -e "\033[1;34m[$(date +%H:%M:%S)]\033[0m $*"; }
warn() { echo -e "\033[1;33m[警告]\033[0m $*"; }
die()  { echo -e "\033[1;31m[错误]\033[0m $*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        --image)   IMAGE="$2"; shift 2 ;;
        --device)  DEVICE="$2"; shift 2 ;;
        --yes)     SKIP_CONFIRM="yes"; shift ;;
        -h|--help)
            echo "用法: sudo ./make-usb.sh --image <镜像> --device <设备> [--yes]"
            exit 0 ;;
        *) die "未知参数: $1" ;;
    esac
done

[[ $EUID -eq 0 ]] || die "必须用 root 运行"
[[ -n "$IMAGE" ]]  || die "必须指定 --image"
[[ -f "$IMAGE" ]]  || die "镜像不存在: $IMAGE"
[[ -n "$DEVICE" ]] || die "必须指定 --device（如 /dev/sdb）"
[[ -b "$DEVICE" ]] || die "不是块设备: $DEVICE"

# ============================================================================
#  安全检查：绝不写到系统盘
# ============================================================================
ROOT_DISK="$(lsblk -no PKNAME "$(findmnt -n -o SOURCE /)" 2>/dev/null || echo "")"
if [[ -n "$ROOT_DISK" && "/dev/$ROOT_DISK" == "$DEVICE" ]]; then
    die "拒绝执行：$DEVICE 是当前系统盘！"
fi

log "=========================================="
log " U 盘刷机"
log "=========================================="
log " 镜像  : $IMAGE"
log " 大小  : $(du -h "$IMAGE" | cut -f1)"
log " 设备  : $DEVICE"
log " 容量  : $(lsblk -no SIZE "$DEVICE" | head -1)"
log " 型号  : $(lsblk -no MODEL "$DEVICE" 2>/dev/null | head -1)"
log "=========================================="
echo
lsblk "$DEVICE"
echo

if [[ "$SKIP_CONFIRM" != "yes" ]]; then
    warn "⚠️  这会【彻底擦除】$DEVICE 上的所有数据！"
    read -r -p "确认继续？输入大写 YES: " ans
    [[ "$ans" == "YES" ]] || { log "已取消"; exit 0; }
fi

# ============================================================================
#  校验
# ============================================================================
if [[ -f "${IMAGE}.sha256" ]]; then
    log "校验镜像..."
    (cd "$(dirname "$IMAGE")" && sha256sum -c "$(basename "${IMAGE}.sha256")") \
        || die "SHA256 校验失败，镜像可能已损坏"
    log "  ✅ 校验通过"
fi

# ============================================================================
#  写入
# ============================================================================
log "写入中（约需 3-10 分钟，取决于 U 盘速度）..."
dd if="$IMAGE" of="$DEVICE" bs=4M status=progress conv=fsync
sync

log "刷新内核分区表..."
partprobe "$DEVICE" 2>/dev/null || true
sleep 2

# ============================================================================
#  回读校验
# ============================================================================
log "回读校验..."
IMG_SIZE="$(stat -c %s "$IMAGE")"
READ_HASH="$(dd if="$DEVICE" bs=4M count=$(( (IMG_SIZE + 4194303) / 4194304 )) 2>/dev/null | head -c "$IMG_SIZE" | sha256sum | cut -d' ' -f1)"
IMG_HASH="$(sha256sum "$IMAGE" | cut -d' ' -f1)"

if [[ "$READ_HASH" == "$IMG_HASH" ]]; then
    log "  ✅ 校验通过"
else
    die "回读校验失败！U 盘可能有坏块，换一个 U 盘重试"
fi

log "=========================================="
log " ✅ 刷机 U 盘制作完成"
log "=========================================="
log ""
log " 安装步骤："
log "   1. U 盘插入一体机"
log "   2. BIOS 设置 U 盘启动（关闭 Secure Boot）"
log "   3. 开机进入安装界面"
log "   4. 选择目标硬盘 → 自动写入"
log "   5. 拔 U 盘，重启 → 进入收银界面"
log "=========================================="
