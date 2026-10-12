#!/usr/bin/env bash
#
# 逸华餐饮收银 —— A/B 分区升级 / 回滚
#
# 用法：
#   sudo yihua-upgrade.sh --image /data/upgrade/yihua-pos-1.1.0-amd64.img
#   sudo yihua-upgrade.sh --rollback
#   sudo yihua-upgrade.sh --status
#
set -euo pipefail

DATA_DIR="${DSH_POS_DATA:-/data}"
STATE_FILE="$DATA_DIR/upgrade-state.json"
GRUB_DEFAULT_FILE="/etc/default/grub"

log()  { echo -e "\033[1;34m[$(date +%H:%M:%S)]\033[0m $*"; }
die()  { echo -e "\033[1;31m[错误]\033[0m $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "必须用 root 运行"

# ============================================================================
#  当前启动的是哪个分区
# ============================================================================
current_slot() {
    local root_dev
    root_dev="$(findmnt -n -o SOURCE / )"
    local label
    label="$(blkid -s LABEL -o value "$root_dev" 2>/dev/null || echo "")"

    case "$label" in
        rootfs-A) echo "A" ;;
        rootfs-B) echo "B" ;;
        *) die "无法识别当前根分区（LABEL=$label）" ;;
    esac
}

other_slot() {
    [[ "$1" == "A" ]] && echo "B" || echo "A"
}

slot_device() {
    local label="rootfs-$1"
    local dev
    dev="$(blkid -L "$label" 2>/dev/null)" || die "找不到分区 $label"
    echo "$dev"
}

# ============================================================================
#  状态
# ============================================================================
show_status() {
    local cur
    cur="$(current_slot)"
    echo "当前启动分区 : $cur"
    echo "备用分区     : $(other_slot "$cur")"
    echo "备用分区设备 : $(slot_device "$(other_slot "$cur")")"
    if [[ -f "$STATE_FILE" ]]; then
        echo "升级状态     :"
        cat "$STATE_FILE"
    else
        echo "升级状态     : 无记录"
    fi
}

# ============================================================================
#  升级
# ============================================================================
do_upgrade() {
    local image="$1"
    [[ -f "$image" ]] || die "镜像不存在: $image"

    log "校验镜像..."
    if [[ -f "${image}.sha256" ]]; then
        (cd "$(dirname "$image")" && sha256sum -c "$(basename "${image}.sha256")") \
            || die "SHA256 校验失败"
        log "  ✅ 校验通过"
    else
        log "  ⚠️ 没有 .sha256 文件，跳过校验"
    fi

    local cur target_label target_dev
    cur="$(current_slot)"
    target_label="$(other_slot "$cur")"
    target_dev="$(slot_device "$target_label")"

    log "写入分区 rootfs-$target_label（$target_dev）..."
    log "  这一步约需 1-3 分钟，请勿断电"

    # 解包镜像里的 rootfs 分区到目标分区
    # 镜像布局：p1=bios_grub  p2=boot  p3=rootfs-A  p4=rootfs-B  p5=data
    local loop
    loop="$(losetup --show -fP "$image")"
    trap 'losetup -d "$loop" 2>/dev/null || true' RETURN

    dd if="${loop}p3" of="$target_dev" bs=4M status=progress conv=fsync
    sync

    log "切换启动项 → rootfs-$target_label"
    sed -i "s/^GRUB_DEFAULT=.*/GRUB_DEFAULT=yihua-$(echo "$target_label" | tr 'A-Z' 'a-z')/" \
        "$GRUB_DEFAULT_FILE"
    update-grub

    # 记录状态（用于启动失败自动回滚）
    cat > "$STATE_FILE" <<EOF
{
  "pending_slot": "$target_label",
  "previous_slot": "$cur",
  "image": "$(basename "$image")",
  "time": "$(date '+%F %T')",
  "boot_attempts": 0
}
EOF

    log "=========================================="
    log " ✅ 升级已写入，重启后生效"
    log "=========================================="
    log " 新系统: rootfs-$target_label"
    log " 如果启动失败，会自动回滚到 rootfs-$cur"
    log ""
    read -r -p "现在重启？(y/N) " ans
    [[ "$ans" =~ ^[Yy]$ ]] && reboot
}

# ============================================================================
#  回滚
# ============================================================================
do_rollback() {
    local cur
    cur="$(current_slot)"
    local back
    back="$(other_slot "$cur")"

    log "回滚：$cur → $back"
    sed -i "s/^GRUB_DEFAULT=.*/GRUB_DEFAULT=yihua-$(echo "$back" | tr 'A-Z' 'a-z')/" \
        "$GRUB_DEFAULT_FILE"
    update-grub

    rm -f "$STATE_FILE"
    log "✅ 已切换回 rootfs-$back，重启生效"
}

# ============================================================================
#  启动成功确认（由 yihua-pos.service 调用）
# ============================================================================
confirm_boot() {
    [[ -f "$STATE_FILE" ]] || return 0

    local pending
    pending="$(grep -o '"pending_slot": *"[AB]"' "$STATE_FILE" | grep -o '[AB]')"
    local cur
    cur="$(current_slot)"

    if [[ "$pending" == "$cur" ]]; then
        log "新系统启动成功，清除升级标记"
        rm -f "$STATE_FILE"
    fi
}

# ============================================================================
#  参数解析
# ============================================================================
case "${1:-}" in
    --image)    do_upgrade "${2:?缺少镜像路径}" ;;
    --rollback) do_rollback ;;
    --status)   show_status ;;
    --confirm)  confirm_boot ;;
    *) cat <<EOF
用法:
  yihua-upgrade.sh --image <镜像路径>   升级到新版本
  yihua-upgrade.sh --rollback           回滚到上一个版本
  yihua-upgrade.sh --status             查看当前状态
  yihua-upgrade.sh --confirm            确认启动成功（系统调用）
EOF
       exit 1 ;;
esac
