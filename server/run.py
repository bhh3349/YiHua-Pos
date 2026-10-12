#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""启动服务端：python run.py [--port 8000] [--reload]"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import uvicorn  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="0.0.0.0")
    ap.add_argument("--port", type=int, default=8000)
    ap.add_argument("--reload", action="store_true")
    a = ap.parse_args()

    frozen = getattr(sys, "frozen", False)
    print("=" * 60)
    print("自有餐饮 POS 服务端" + ("  [打包模式]" if frozen else ""))
    print("  新契约 : http://127.0.0.1:%d/api/v1/{domain}/{action}" % a.port)
    print("  兼容层 : http://127.0.0.1:%d/saas/login  (参考系统客户端直连)" % a.port)
    print("  静态   : http://127.0.0.1:%d/oss/" % a.port)
    print("  文档   : http://127.0.0.1:%d/docs" % a.port)
    print("=" * 60)

    if a.reload and not frozen:
        # reload 需要 import 字符串
        uvicorn.run("app.main:app", host=a.host, port=a.port, reload=True)
    else:
        # 直接传 app 对象：打包后不依赖 import 字符串
        from app.main import app
        uvicorn.run(app, host=a.host, port=a.port, log_level="info")


if __name__ == "__main__":
    main()
