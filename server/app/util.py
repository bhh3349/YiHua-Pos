"""公共工具：响应封装、上下文、编号、CSV。"""
import csv
import hashlib
import os
import uuid
from datetime import datetime
from typing import Any, Optional

from fastapi import Header, HTTPException, Request
from fastapi.responses import JSONResponse

from .config import OSS_BASE_URL, OSS_DIR


# ---------------- 响应封装 ----------------
def ok(data: Any = None, message: str = "ok") -> dict:
    return {"code": "200", "message": message, "data": data}


def fail(code: str, message: str, data: Any = None) -> dict:
    return {"code": code, "message": message, "data": data}


class BizError(Exception):
    """业务异常：会被全局处理器转成标准响应体。"""

    def __init__(self, code: str, message: str, data: Any = None):
        self.code, self.message, self.data = code, message, data


async def biz_error_handler(request: Request, exc: BizError) -> JSONResponse:
    trace = request.headers.get("X-Trace-No") or uuid.uuid4().hex
    return JSONResponse(status_code=200, content={
        "code": exc.code, "message": exc.message, "data": exc.data,
        "traceNo": trace, "timestamp": int(datetime.now().timestamp() * 1000),
    })


# ---------------- 上下文 ----------------
class Ctx:
    def __init__(self, user_id: int, tenant_id: int, store_id: int, terminal_no: str, client: str):
        self.user_id, self.tenant_id, self.store_id = user_id, tenant_id, store_id
        self.terminal_no, self.client = terminal_no, client


def get_ctx(
    authorization: Optional[str] = Header(None),
    x_tenant_id: Optional[str] = Header(None),
    x_store_id: Optional[str] = Header(None),
    x_terminal_no: Optional[str] = Header(None),
    x_client: Optional[str] = Header("win"),
) -> Ctx:
    from .security import decode_token

    if not authorization or not authorization.lower().startswith("bearer "):
        raise BizError("401", "未登录")
    payload = decode_token(authorization.split(" ", 1)[1].strip())
    if not payload or payload.get("typ") != "access":
        raise BizError("401", "登录已过期")

    tenant_id = int(payload.get("tid") or x_tenant_id or 0)
    store_id = int(x_store_id or 0) or int(payload.get("sid") or 0)
    return Ctx(int(payload.get("sub") or 0), tenant_id, store_id,
               x_terminal_no or payload.get("term") or "", x_client or "win")


# ---------------- 编号 ----------------
def md5_16(s: Any) -> str:
    """与客户端 Md5Util.Md5_16 一致：MD5 十六进制串的**前 16 位**（大写无关，客户端未转小写）"""
    return hashlib.md5(str(s).encode("utf-8")).hexdigest()[:16]


def gen_order_no(store_id: int) -> str:
    return "%d%s%s" % (store_id, datetime.now().strftime("%y%m%d%H%M%S"), uuid.uuid4().hex[:4].upper())


def gen_trade_no(prefix: str = "T") -> str:
    return prefix + datetime.now().strftime("%Y%m%d%H%M%S") + uuid.uuid4().hex[:6].upper()


# ---------------- CSV / 对象存储 ----------------
def oss_dir_for(tenant_id: Any, store_id: Any, sub: str = "catalog") -> str:
    return os.path.join(OSS_DIR, md5_16(tenant_id), md5_16(store_id), sub)


def write_csv(path: str, header: list, rows: list) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(header)
        for r in rows:
            w.writerow(r)


def oss_url(path: str) -> str:
    rel = os.path.relpath(path, OSS_DIR).replace("\\", "/")
    return OSS_BASE_URL.rstrip("/") + "/" + rel
