"""SQLite migration coverage for legacy order idempotency constraints."""
from sqlalchemy import create_engine
from sqlalchemy.schema import CreateTable

from app.models import Order
from migrations.migrate_order_unique import detect, migrate_order_unique


def _legacy_order_engine():
    engine = create_engine("sqlite://")
    ddl = str(CreateTable(Order.__table__).compile(engine))
    ddl = ddl.replace("uk_store_client_no", "uk_client_no")
    ddl = ddl.replace("UNIQUE (store_id, client_order_no)", "UNIQUE (client_order_no)")
    with engine.begin() as conn:
        conn.exec_driver_sql(ddl)
        conn.exec_driver_sql(
            "INSERT INTO t_order (id, tenant_id, store_id, order_no, client_order_no, status) "
            "VALUES (1, 10, 20, 'O-1', 'C-1', 0)"
        )
        conn.exec_driver_sql(
            "INSERT INTO t_order (id, tenant_id, store_id, order_no, client_order_no, status) "
            "VALUES (2, 10, 21, 'O-2', 'C-2', 1)"
        )
    return engine


def test_order_unique_migration_preserves_rows_and_is_idempotent():
    engine = _legacy_order_engine()
    try:
        assert detect(engine) == "needs-migration"
        result = migrate_order_unique(engine)
        assert result["migrated"] is True
        assert result["rows"] == 2
        assert detect(engine) == "migrated"

        with engine.connect() as conn:
            rows = conn.exec_driver_sql(
                "SELECT id, tenant_id, store_id, order_no, client_order_no, status "
                "FROM t_order ORDER BY id"
            ).fetchall()
            assert [tuple(row) for row in rows] == [
                (1, 10, 20, "O-1", "C-1", 0),
                (2, 10, 21, "O-2", "C-2", 1),
            ]

        assert migrate_order_unique(engine)["migrated"] is False
    finally:
        engine.dispose()


def test_order_unique_migration_dry_run_does_not_change_schema():
    engine = _legacy_order_engine()
    try:
        result = migrate_order_unique(engine, dry_run=True)
        assert result["reason"] == "dry-run"
        assert detect(engine) == "needs-migration"
    finally:
        engine.dispose()
