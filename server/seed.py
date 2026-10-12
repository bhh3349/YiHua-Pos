#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
演示数据初始化（CLI 包装，逻辑在 app/seed_data.py）
服务端首次启动会自动初始化；本脚本用于手动重置。

用法：
    python seed.py            # 幂等
    python seed.py --reset    # 先清库再灌
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from app.db import SessionLocal, init_db  # noqa: E402
from app.models import (Desk, DeskArea, Dictionary, Member,  # noqa: E402
                        MemberBalanceLog, Order, OrderFlow, OrderItem, Payment,
                        Product, ProductCategory, ProductUnit, Shift, Store,
                        SysRole, SysUser, Tenant, Terminal)
from app.seed_data import seed            # noqa: E402


def reset(db):
    """清空全部业务数据（含订单/流水），让 smoke_test 可以反复跑。"""
    for m in (OrderItem, OrderFlow, Payment, MemberBalanceLog, Order, Shift,
              Dictionary, Desk, DeskArea, Member, Product, ProductCategory,
              ProductUnit, Terminal, SysUser, SysRole, Store, Tenant):
        db.query(m).delete()
    db.commit()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--reset", action="store_true")
    a = ap.parse_args()

    init_db()
    db = SessionLocal()
    try:
        if a.reset:
            reset(db)
            print("已清空演示数据")
        info = seed(db)
        print("=" * 56)
        print("演示数据就绪")
        print("  租户: %s" % info["tenant"])
        print("  门店: %s" % info["store"])
        print("  账号: %s / %s" % (info["account"], info["password"]))
        print("  商品 %d 个，桌台 %d 个，会员 %d 个"
              % (info["products"], info["desks"], info["members"]))
        print("=" * 56)
    finally:
        db.close()


if __name__ == "__main__":
    main()
