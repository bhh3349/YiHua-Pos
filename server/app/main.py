"""FastAPI 应用装配。"""
import os
import uuid
from datetime import datetime

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles

from .config import (APP_NAME, IS_PROD, JWT_SECRET_SOURCE, JWT_SECRET_WARNING,
                     OSS_DIR)
from .db import init_db
from .routers import (auth, catalog, desk, groupbuy, legacy, legacy_order,
                      legacy_print, member, order, pay, report, shift)
from .util import BizError, biz_error_handler

app = FastAPI(title=APP_NAME, version="0.1.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"],
)


@app.middleware("http")
async def envelope_middleware(request: Request, call_next):
    """统一补 traceNo / timestamp，保证所有响应形态一致。"""
    response = await call_next(request)
    return response


@app.middleware("http")
async def access_log(request: Request, call_next):
    """把每个请求写进 data/access.log，便于排查客户端调了什么、卡在哪一步"""
    start = datetime.now()
    response = await call_next(request)
    line = "[%s] %s %s -> %d\n" % (start.strftime("%H:%M:%S"), request.method,
                                   request.url.path, response.status_code)
    try:
        from .config import DATA_DIR
        with open(os.path.join(DATA_DIR, "access.log"), "a", encoding="utf-8") as f:
            f.write(line)
    except Exception:
        pass
    return response


@app.exception_handler(BizError)
async def _biz(request: Request, exc: BizError):
    return await biz_error_handler(request, exc)


@app.exception_handler(RequestValidationError)
async def _validation(request: Request, exc: RequestValidationError):
    """参数校验失败也回标准响应体。

    客户端只认 {code, message, data}，裸的 FastAPI 422（{detail:[...]}）
    会让它解析失败、直接卡住；这里统一成业务错误形态。
    """
    trace = request.headers.get("X-Trace-No") or uuid.uuid4().hex
    first = (exc.errors() or [{}])[0]
    loc = ".".join(str(x) for x in (first.get("loc") or []) if x != "body")
    return JSONResponse(status_code=200, content={
        "code": "422",
        "message": "参数错误: %s %s" % (loc, first.get("msg") or ""),
        "data": None,
        "traceNo": trace,
        "timestamp": int(datetime.now().timestamp() * 1000),
    })


@app.exception_handler(Exception)
async def _unhandled(request: Request, exc: Exception):
    trace = request.headers.get("X-Trace-No") or uuid.uuid4().hex
    return JSONResponse(status_code=200, content={
        "code": "500", "message": "服务端异常: %s" % exc, "data": None,
        "traceNo": trace, "timestamp": int(datetime.now().timestamp() * 1000),
    })


def _check_jwt_secret() -> None:
    """启动时把 JWT 密钥的来路讲清楚。

    密钥本身在 config 导入时就已经校验过（生产环境不合格会直接抛错、
    服务端起不来），这里只负责把开发环境的提示打到控制台上 ——
    控制台里藏着一条「这是开发密钥」比出事后再查强得多。
    """
    if IS_PROD:
        print("[安全] JWT 密钥来源: %s（生产模式）" % JWT_SECRET_SOURCE)
        return
    if JWT_SECRET_WARNING:
        line = "!" * 68
        print(line)
        print("[安全警告] %s" % JWT_SECRET_WARNING)
        print(line)


@app.on_event("startup")
def _startup():
    _check_jwt_secret()
    os.makedirs(OSS_DIR, exist_ok=True)
    init_db()
    from .seed_data import seed_if_empty
    seed_if_empty()
    try:
        from .legacy_csv import export_legacy_csv
        r = export_legacy_csv()
        if r.get("ok"):
            print("[启动] 已导出兼容商品 CSV: %d 个商品" % r["products"])
            print("       租户目录 %s" % r["tenantDir"])
            print("       门店目录 %s" % r["storeDir"])
    except Exception as e:
        print("[启动] 导出兼容 CSV 失败: %r" % e)


@app.get("/health")
def health():
    return {"ok": True, "app": APP_NAME, "time": datetime.now().strftime("%Y-%m-%d %H:%M:%S")}


# ---- 业务路由（新契约）----
for r in (auth.router, catalog.router, desk.router, order.router,
          pay.router, shift.router, member.router, report.router,
          groupbuy.router):
    app.include_router(r)

# ---- 静态资源（商品 CSV，等价于对象存储）----
os.makedirs(OSS_DIR, exist_ok=True)
app.mount("/oss", StaticFiles(directory=OSS_DIR), name="oss")

# ---- 兼容层放最后：catch-all 不能吃掉上面的路由 ----
# 顺序要求：legacy_order / legacy_print 必须在 legacy 之前，
# 因为 legacy.router 末尾有 POST /{full_path:path} 兜底。
app.include_router(legacy_order.router)
app.include_router(legacy_print.router)
app.include_router(legacy.router)
