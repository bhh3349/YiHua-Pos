#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
导出「参考系统客户端兼容格式」的商品 CSV（CLI 包装，逻辑在 app/legacy_csv.py）

服务端启动时会自动执行一次；本脚本用于手动重跑。

用法：
    python export_legacy_csv.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from app.db import init_db            # noqa: E402
from app.legacy_csv import export_legacy_csv  # noqa: E402


def main():
    init_db()
    r = export_legacy_csv()
    if not r.get("ok"):
        print("失败: %s" % r.get("reason"))
        return 1
    print("租户目录: %s" % r["tenantDir"])
    print("门店目录: %s" % r["storeDir"])
    print("商品行数: %d" % r["products"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
