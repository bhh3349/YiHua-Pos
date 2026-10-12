#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
逸华餐饮收银 —— 一体化启动器

职责：
    1. 启动本地服务端（Server/pos-server.exe），等待健康检查通过
    2. 启动收银客户端（Client/<POS_CLIENT_EXE>）
    3. 监控两者：服务端挂了自动拉起；客户端退出则一并关闭服务端
    4. 全程写日志到 logs/launcher.log

目录约定：
    YiHua-POS/
    ├── 启动收银.exe          <- 本程序
    ├── Server/pos-server.exe
    ├── Client/               <- 收银端（文件名可用 POS_CLIENT_EXE 覆盖）
    ├── data/                 <- 服务端数据（自动创建）
    └── logs/
"""
import ctypes
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime

PORT = int(os.getenv("POS_PORT", "8000"))
HOST = "127.0.0.1"

if getattr(sys, "frozen", False):
    ROOT = os.path.dirname(sys.executable)
else:
    ROOT = os.path.dirname(os.path.abspath(__file__))

SERVER_EXE = os.path.join(ROOT, "Server", "pos-server.exe")
# 收银端可执行文件名可通过环境变量覆盖（便于换品牌/换文件名）
CLIENT_NAME = os.getenv("POS_CLIENT_EXE", "YiHua.Cashier.exe")
CLIENT_EXE = os.path.join(ROOT, "Client", CLIENT_NAME)
LOG_DIR = os.path.join(ROOT, "logs")
LOG_FILE = os.path.join(LOG_DIR, "launcher.log")

os.makedirs(LOG_DIR, exist_ok=True)


def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%Y-%m-%d %H:%M:%S"), msg)
    print(line, flush=True)
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except Exception:
        pass


def health(timeout=3):
    try:
        with urllib.request.urlopen("http://%s:%d/health" % (HOST, PORT), timeout=timeout) as r:
            return b'"ok"' in r.read()
    except Exception:
        return False


def start_server():
    if health():
        log("检测到服务端已在运行，直接复用")
        return None
    if not os.path.exists(SERVER_EXE):
        log("!! 找不到服务端: %s" % SERVER_EXE)
        return None
    log("启动本地服务端 ...")
    flags = 0
    if sys.platform == "win32":
        flags = subprocess.CREATE_NO_WINDOW
    p = subprocess.Popen([SERVER_EXE, "--port", str(PORT), "--host", HOST],
                         cwd=os.path.dirname(SERVER_EXE), creationflags=flags,
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    log("服务端 PID = %d，等待就绪 ..." % p.pid)
    for i in range(60):
        if health():
            log("服务端就绪（耗时 %.1fs）" % ((i + 1) * 0.5))
            return p
        if p.poll() is not None:
            log("!! 服务端进程已退出，退出码 %s" % p.returncode)
            return None
        time.sleep(0.5)
    log("!! 服务端 30 秒内未就绪")
    return p


def start_client():
    if not os.path.exists(CLIENT_EXE):
        log("!! 找不到客户端: %s" % CLIENT_EXE)
        return None
    log("启动收银客户端 ...")
    return subprocess.Popen([CLIENT_EXE], cwd=os.path.dirname(CLIENT_EXE))


def main():
    print("=" * 60)
    print("  逸华餐饮收银  ——  本地一体化启动")
    print("  服务端: http://%s:%d" % (HOST, PORT))
    print("=" * 60)
    log("启动器开始运行 (ROOT=%s)" % ROOT)

    server = start_server()
    if server is None and not health():
        log("服务端不可用，退出")
        input("按回车退出 ...")
        return 1

    client = start_client()
    if client is None:
        log("客户端启动失败，退出")
        if server:
            server.terminate()
        input("按回车退出 ...")
        return 1

    log("客户端 PID = %d" % client.pid)
    log("进入监控循环（关闭收银端窗口即全部退出）")

    while True:
        time.sleep(3)
        if client.poll() is not None:
            log("客户端已退出（退出码 %s），关闭服务端" % client.returncode)
            break
        if server is not None and server.poll() is not None:
            log("!! 服务端意外退出（退出码 %s），尝试重启" % server.returncode)
            server = start_server()

    if server is not None and server.poll() is None:
        server.terminate()
        try:
            server.wait(timeout=8)
        except Exception:
            server.kill()
    log("已全部退出")
    return 0


if __name__ == "__main__":
    sys.exit(main())
