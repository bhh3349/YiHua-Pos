"""验券码限流 —— 防刷（任务卡 4.7）。

滑动窗口计数，内存实现（单机部署的服务端够用；上多实例时换 Redis 只改
这一个文件，业务路由不受影响）。窗口内同一门店验券次数超限即回 RATE_LIMITED。
"""
import os
import threading
import time
from collections import defaultdict, deque

from .errors import GroupbuyError, PlatformErrorCode

_LIMIT = 30        # 每窗口每门店最多验券次数
_WINDOW = 60.0     # 窗口宽度（秒）

_lock = threading.Lock()
_hits = defaultdict(deque)      # key -> deque[timestamp]


def _limit() -> int:
    """限额读环境变量，测试里可放大以免并发用例撞限流（生产不设即用默认）。"""
    try:
        return max(1, int(os.getenv("DSH_POS_GROUPBUY_RATE_LIMIT", _LIMIT)))
    except ValueError:
        return _LIMIT


def _window() -> float:
    try:
        return max(1.0, float(os.getenv("DSH_POS_GROUPBUY_RATE_WINDOW", _WINDOW)))
    except ValueError:
        return _WINDOW


def check_rate_limit(key: str, limit: int = None, window: float = None) -> None:
    """超限抛 RATE_LIMITED；key 一般用 ``store_id``，也可加维度。"""
    limit = _limit() if limit is None else limit
    window = _window() if window is None else window
    now = time.monotonic()
    with _lock:
        q = _hits[key]
        while q and q[0] <= now - window:
            q.popleft()
        if len(q) >= limit:
            raise GroupbuyError(PlatformErrorCode.RATE_LIMITED)
        q.append(now)


def reset_rate_limit() -> None:
    """测试用：清空计数。"""
    with _lock:
        _hits.clear()
