#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""迁移：t_order 的幂等键从「全局唯一」改成「按门店唯一」。

    uk_client_no        UNIQUE (client_order_no)
        ↓
    uk_store_client_no  UNIQUE (store_id, client_order_no)

背景：老索引不带 store_id，两个门店用同一个 clientOrderNo 各建一单时，
第二个门店会被数据库直接拦下。``models.py`` 已经改成复合唯一，
但 **SQLite 不支持 ALTER TABLE ... DROP CONSTRAINT**，已存在的库必须重建表：

    旧表改名 → 按当前模型建新表 → 逐列拷贝数据 → 删旧表

用法::

    python -m migrations.migrate_order_unique            # 执行迁移
    python -m migrations.migrate_order_unique --dry-run  # 只看要不要迁

幂等：库里已经是新约束（或压根没有 t_order）时直接返回，不做任何改动。
"""
import argparse
import sys
from typing import Dict, List, Optional

from sqlalchemy import Engine

TABLE = "t_order"
OLD_TABLE = "t_order__old"
OLD_CONSTRAINT = "uk_client_no"
NEW_CONSTRAINT = "uk_store_client_no"


# --------------------------------------------------------------- 探测

def _table_ddl(conn, table: str = TABLE) -> Optional[str]:
    """取建表语句；表不存在返回 None。约束名只能从 DDL 文本里认。"""
    row = conn.exec_driver_sql(
        "SELECT sql FROM sqlite_master WHERE type='table' AND name=?", (table,)
    ).fetchone()
    return row[0] if row else None


def _explicit_indexes(conn, table: str = TABLE) -> List[str]:
    """显式 CREATE INDEX 建出来的索引名。

    隐式索引（sqlite_autoindex_*）会随旧表一起消失，不用管；
    显式索引在 ALTER TABLE RENAME 之后仍然占着名字，重建前必须先删掉。
    """
    names = []
    for row in conn.exec_driver_sql('PRAGMA index_list("%s")' % table):
        name = row[1]
        if name and not name.startswith("sqlite_autoindex"):
            names.append(name)
    return names


def _columns(conn, table: str) -> List[str]:
    return [r[1] for r in conn.exec_driver_sql('PRAGMA table_info("%s")' % table)]


def _snapshot(conn, table: str = TABLE) -> List[tuple]:
    """整表快照（按 id 排序），用来比对迁移前后是否一致。"""
    cols = _columns(conn, table)
    collist = ", ".join('"%s"' % c for c in cols)
    rows = conn.exec_driver_sql(
        'SELECT %s FROM "%s" ORDER BY id' % (collist, table)).fetchall()
    return [tuple(r) for r in rows]


def detect(engine: Engine) -> str:
    """返回库的当前状态：absent / migrated / needs-migration / unknown。"""
    if engine.dialect.name != "sqlite":
        return "non-sqlite"
    with engine.connect() as conn:
        ddl = _table_ddl(conn)
        if ddl is None:
            return "absent"
        if OLD_CONSTRAINT in ddl:
            return "needs-migration"
        if NEW_CONSTRAINT in ddl:
            return "migrated"
        return "unknown"


# --------------------------------------------------------------- 迁移

def migrate_order_unique(engine: Engine, dry_run: bool = False) -> Dict:
    """把 t_order 的幂等键迁成复合唯一。返回结果字典，幂等。"""
    state = detect(engine)
    if state == "non-sqlite":
        return {"ok": True, "migrated": False, "reason": state,
                "message": "仅 SQLite 需要重建表；%s 请走正式 DDL 变更"
                           % engine.dialect.name}
    if state == "absent":
        return {"ok": True, "migrated": False, "reason": state,
                "message": "库中还没有 %s，无需迁移" % TABLE}
    if state == "migrated":
        return {"ok": True, "migrated": False, "reason": state,
                "message": "%s 已是复合唯一约束" % NEW_CONSTRAINT}
    if state == "unknown":
        return {"ok": True, "migrated": False, "reason": state,
                "message": "%s 的建表语句里既没有 %s 也没有 %s，"
                           "结构未知，不做处理（请人工确认）"
                           % (TABLE, OLD_CONSTRAINT, NEW_CONSTRAINT)}

    from app.models import Order  # 延迟导入：避免 app.db -> migrations -> app.models 循环

    with engine.begin() as conn:
        before = _snapshot(conn)
        if dry_run:
            return {"ok": True, "migrated": False, "reason": "dry-run",
                    "rows": len(before),
                    "message": "需要迁移 %d 行（dry-run 未改动）" % len(before)}

        dropped_indexes = _explicit_indexes(conn)
        for name in dropped_indexes:
            conn.exec_driver_sql('DROP INDEX IF EXISTS "%s"' % name)

        conn.exec_driver_sql('ALTER TABLE "%s" RENAME TO "%s"' % (TABLE, OLD_TABLE))
        # 按当前模型重建：CREATE TABLE + idx_store_time / idx_status 一起建出来
        Order.__table__.create(bind=conn)

        # 只拷两边都有的列：旧库少列时新列取默认值，多列时丢弃
        old_cols = set(_columns(conn, OLD_TABLE))
        new_cols = [c.name for c in Order.__table__.columns]
        shared = [c for c in new_cols if c in old_cols]
        if "id" not in shared:                       # pragma: no cover - 防御
            raise RuntimeError("旧表缺少 id 列，拒绝迁移")
        collist = ", ".join('"%s"' % c for c in shared)
        conn.exec_driver_sql(
            'INSERT INTO "%s" (%s) SELECT %s FROM "%s"'
            % (TABLE, collist, collist, OLD_TABLE))
        conn.exec_driver_sql('DROP TABLE "%s"' % OLD_TABLE)

        after = _snapshot(conn)
        if before != after:
            # 事务里抛异常 → 整体回滚，旧表原样还在
            raise RuntimeError("迁移前后数据不一致（%d 行 -> %d 行），已回滚"
                               % (len(before), len(after)))

        ddl = _table_ddl(conn)
        if NEW_CONSTRAINT not in (ddl or ""):        # pragma: no cover - 防御
            raise RuntimeError("新表没有建出 %s，已回滚" % NEW_CONSTRAINT)

        return {"ok": True, "migrated": True, "reason": "migrated",
                "rows": len(after), "droppedIndexes": dropped_indexes,
                "columns": shared,
                "message": "已把 %s 的幂等键重建为 %s(store_id, client_order_no)，"
                           "迁移 %d 行" % (TABLE, NEW_CONSTRAINT, len(after))}


# --------------------------------------------------------------- CLI

def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="t_order 幂等键迁移（全局唯一 -> 复合唯一）")
    ap.add_argument("--dry-run", action="store_true", help="只检查，不改动")
    a = ap.parse_args(argv)

    import os
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

    from app.db import engine
    r = migrate_order_unique(engine, dry_run=a.dry_run)
    print("[%s] %s" % (r["reason"], r["message"]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
