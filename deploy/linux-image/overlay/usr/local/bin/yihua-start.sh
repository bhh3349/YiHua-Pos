#!/usr/bin/env bash
#
# 逸华餐饮收银 —— 客户端启动脚本
# 由 systemd 的 yihua-pos.service 调用
#
set -euo pipefail

APP_DIR="/opt/yihua/app"
DATA_DIR="${DSH_POS_DATA:-/data}"
LOG_DIR="${DSH_POS_LOG:-$DATA_DIR/logs}"
DOTNET_ROOT="${DOTNET_ROOT:-/opt/dotnet}"

export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"

mkdir -p "$LOG_DIR"

log() { echo "[$(date '+%F %T')] $*"; }

# ============================================================================
#  1. 准备可写目录（根文件系统只读，这些挂 tmpfs）
# ============================================================================
prepare_writable_dirs() {
    for d in /tmp /var/tmp /var/log /var/run /run/user; do
        if ! mountpoint -q "$d" 2>/dev/null; then
            mount -t tmpfs -o size=128M,mode=0755 tmpfs "$d" 2>/dev/null || true
        fi
    done
    mkdir -p /run/user/"$(id -u yihua 2>/dev/null || echo 1000)"
}

# ============================================================================
#  2. 等待外设就绪（打印机/电子秤插上后需要时间枚举）
# ============================================================================
wait_for_devices() {
    local waited=0
    while [[ $waited -lt 10 ]]; do
        # 有串口设备或 USB 打印设备就算就绪
        if compgen -G "/dev/ttyUSB*" >/dev/null 2>&1 \
           || compgen -G "/dev/ttyS[0-9]*" >/dev/null 2>&1 \
           || compgen -G "/dev/usb/lp*" >/dev/null 2>&1; then
            log "外设已就绪（等待 ${waited}s）"
            return 0
        fi
        sleep 1
        waited=$((waited + 1))
    done
    log "未检测到外设，继续启动（可在设置里手动配置）"
}

# ============================================================================
#  3. 图形环境
# ============================================================================
setup_display() {
    # 优先 Wayland，回退 X11
    if [[ -n "${WAYLAND_DISPLAY:-}" ]]; then
        log "使用 Wayland: $WAYLAND_DISPLAY"
        return 0
    fi

    if [[ -z "${DISPLAY:-}" ]]; then
        export DISPLAY=":0"
        log "启动 X11（$DISPLAY）"
        # 后台启动 X（无窗口管理器，应用直接全屏）
        X :0 -nolisten tcp -nocursor vt1 &
        sleep 2
    fi

    # 关闭屏幕保护与 DPMS（收银机不能黑屏）
    xset s off       2>/dev/null || true
    xset -dpms       2>/dev/null || true
    xset s noblank   2>/dev/null || true
}

# ============================================================================
#  4. 数据备份（每次启动前备份一次，保留 7 份）
# ============================================================================
backup_data() {
    local db="$DATA_DIR/yihua.db"
    [[ -f "$db" ]] || return 0

    local backup_dir="$DATA_DIR/backup"
    mkdir -p "$backup_dir"

    local target="$backup_dir/yihua-$(date +%Y%m%d).db"
    [[ -f "$target" ]] && return 0

    cp "$db" "$target" 2>/dev/null && log "已备份数据库 → $target"

    # 只保留最近 7 份
    ls -1t "$backup_dir"/yihua-*.db 2>/dev/null | tail -n +8 | xargs -r rm -f
}

# ============================================================================
#  主流程
# ============================================================================
main() {
    log "=========================================="
    log " 逸华餐饮收银 —— 启动"
    log "=========================================="

    prepare_writable_dirs
    backup_data
    wait_for_devices
    setup_display

    log "启动客户端..."
    cd "$APP_DIR"

    exec "$DOTNET_ROOT/dotnet" "$APP_DIR/YiHua.Pos.dll" "$@"
}

main "$@"
