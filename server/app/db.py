"""数据库会话与基类。"""
from sqlalchemy import create_engine
from sqlalchemy.orm import declarative_base, sessionmaker

from .config import DB_URL

connect_args = ({"check_same_thread": False, "timeout": 30}
                if DB_URL.startswith("sqlite") else {})
engine = create_engine(DB_URL, echo=False, future=True, connect_args=connect_args, pool_pre_ping=True)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def init_db():
    from . import models  # noqa: F401  确保模型已注册
    Base.metadata.create_all(bind=engine)
    _migrate()


def _migrate():
    """老库补齐到当前模型。

    create_all 只建缺的表，**不会**改已存在表的约束；老库的 t_order 还挂着
    全局唯一索引 uk_client_no（跨门店同号会互相撞），必须重建表才能换掉。
    """
    from migrations.migrate_order_unique import migrate_order_unique

    r = migrate_order_unique(engine)
    if r.get("migrated"):
        print("[迁移] %s" % r["message"])
    if r.get("reason") == "unknown":
        raise RuntimeError("[迁移] t_order 约束结构未知，拒绝在未确认的数据库结构上启动")
