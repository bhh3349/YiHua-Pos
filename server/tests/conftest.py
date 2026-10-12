"""pytest 公共夹具。

关键点：``app.config`` 在 **导入时** 就读环境变量并建目录，
所以必须在 import app 之前把 ``DSH_POS_DATA`` 指到临时目录，
否则测试会污染 server/data/pos.db 里的开发数据。
"""
import json
import os
import shutil
import sys
import tempfile

import pytest

SERVER_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if SERVER_DIR not in sys.path:
    sys.path.insert(0, SERVER_DIR)

# ---- 必须在 import app.* 之前设置 ----
_TEST_DATA = tempfile.mkdtemp(prefix="yihua-pos-test-")
os.environ["DSH_POS_DATA"] = _TEST_DATA
os.environ.setdefault("DSH_POS_JWT_SECRET", "test-secret")

from fastapi.testclient import TestClient  # noqa: E402

from app.db import SessionLocal  # noqa: E402
from app.main import app  # noqa: E402


def pytest_sessionfinish(session, exitstatus):
    shutil.rmtree(_TEST_DATA, ignore_errors=True)


@pytest.fixture(scope="session")
def data_dir():
    return _TEST_DATA


@pytest.fixture(scope="session")
def client():
    """带 startup 的 TestClient：startup 会建表 + 灌演示数据。"""
    with TestClient(app) as c:
        yield c


@pytest.fixture()
def db():
    s = SessionLocal()
    try:
        yield s
    finally:
        s.close()


@pytest.fixture(scope="session")
def auth_headers(client):
    """新契约 /api/v1/* 需要 Bearer。"""
    r = client.post("/api/v1/auth/login",
                    json={"account": "admin", "password": "123456"})
    body = r.json()
    assert body.get("code") == "200", body
    return {"Authorization": "Bearer " + body["data"]["token"]}


# --------------------------------------------------------------- 工具函数

def envelope(resp):
    """校验格式二外层结构，返回解码后的 data（第二层 json.loads 已完成）。"""
    body = resp.json()
    assert body["code"] == 200, body
    assert body["success"] is True, body
    assert "key_sign" in body and "trace_no" in body, body
    assert isinstance(body["data"], str), "格式二 data 必须是二次编码的 JSON 字符串"
    return json.loads(body["data"])


def envelope_raw(resp):
    """返回未解码的整个响应体，供格式断言用。"""
    return resp.json()


def post(client, path, body=None, headers=None):
    return client.post(path, json=body if body is not None else {}, headers=headers or {})


def new_client_no(prefix="PYTEST"):
    import uuid
    return "%s-%s" % (prefix, uuid.uuid4().hex[:12])
