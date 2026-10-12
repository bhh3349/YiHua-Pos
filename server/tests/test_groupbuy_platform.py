"""Adapter 架构 —— 任务卡 4.6 的硬要求，用测试证明：

1. 业务路由/注册表代码里没有任何 ``if platform == "xxx"`` 分支（源码扫描）。
2. 加一个新平台 = 只往 services/groupbuy/ 丢一个 adapter 文件，
   不改任何业务代码：本测试运行时动态写入 ``tmp_platform.py``、
   ``reload_adapters()`` 重扫注册表，路由立即可用；删掉文件再重扫即消失。
"""
import inspect
import os

import pytest

from conftest import post

GB = "/api/v1/groupbuy"
PKG_DIR = os.path.dirname(os.path.abspath(__import__(
    "app.services.groupbuy.platform", fromlist=["x"]).__file__))
TMP_ADAPTER_NAME = "tmp_platform_adapter_for_test"


def _setup(client, auth_headers):
    r = post(client, GB + "/configSave",
             {"platformCode": "self", "storeIdList": [200001],
              "signKey": "k" * 20}, auth_headers).json()
    assert r["code"] == "200", r


@pytest.fixture()
def gb(client, auth_headers):
    _setup(client, auth_headers)
    return client, auth_headers


ADAPTER_SOURCE = '''
"""临时插入的测试平台 —— 由 test_groupbuy_platform.py 运行时写入。"""
from .errors import PlatformErrorCode
from .platform import (CouponInfo, CouponQuery, PlatformRedeemRecord,
                       RedeemResult, SkeletonAdapter, VerifyResult,
                       register_adapter)


@register_adapter
class TmpTestPlatformAdapter(SkeletonAdapter):
    """演示用骨架：平台上架前一切操作报「平台未接入」。"""

    platform_code = "tmptest"
'''


@pytest.fixture()
def tmp_adapter():
    """运行时把一个新 adapter 文件丢进包目录，用完删掉。

    用 try/finally 保证断言失败时也清理 —— 临时文件残留会让后续用例
    里的注册表多出一个平台（曾踩过）。
    """
    import sys
    from app.services.groupbuy import platform as pf

    path = os.path.join(PKG_DIR, TMP_ADAPTER_NAME + ".py")
    assert not os.path.exists(path), "上次测试的临时 adapter 没清干净"

    def _cleanup():
        if os.path.exists(path):
            os.remove(path)
        sys.modules.pop("app.services.groupbuy.%s" % TMP_ADAPTER_NAME, None)
        pf.reload_adapters()

    try:
        with open(path, "w", encoding="utf-8") as f:
            f.write(ADAPTER_SOURCE)
        sys.modules.pop("app.services.groupbuy.%s" % TMP_ADAPTER_NAME, None)
        pf.reload_adapters()
        yield pf
    finally:
        _cleanup()


def test_new_platform_by_dropping_one_file(gb, tmp_adapter):
    """新平台 = 一个 adapter 文件：重扫后路由直接能路由到它。"""
    client, h = gb
    pf = tmp_adapter
    assert "tmptest" in pf.registered_platforms()

    # 平台清单里出现新平台（路由层完全没改过）
    lst = post(client, GB + "/platformList", {}, h).json()
    assert lst["code"] == "200", lst
    codes = [x["platformCode"] for x in lst["data"]]
    assert "tmptest" in codes, codes

    # 未配置 -> CONFIG_NOT_FOUND；骨架行为照常生效
    b = post(client, GB + "/coupon/verify",
             {"platformCode": "tmptest", "couponCode": "TESTCODE"}, h).json()
    assert b["code"] == "CONFIG_NOT_FOUND", b


def test_removed_platform_disappears(gb, tmp_adapter):
    """删掉 adapter 文件并重扫后，平台从注册表消失 -> PLATFORM_UNKNOWN。"""
    client, h = gb
    pf = tmp_adapter
    os.remove(os.path.join(PKG_DIR, TMP_ADAPTER_NAME + ".py"))
    import sys
    sys.modules.pop("app.services.groupbuy.%s" % TMP_ADAPTER_NAME, None)
    pf.reload_adapters()
    assert "tmptest" not in pf.registered_platforms()
    b = post(client, GB + "/coupon/verify",
             {"platformCode": "tmptest", "couponCode": "TESTCODE"}, h).json()
    assert b["code"] == "PLATFORM_UNKNOWN", b


def test_no_platform_branch_in_business_code():
    """业务代码里不允许出现 ``if platform == ...`` 这类平台分支。

    按行扫描路由层与注册表/校验层：平台差异只许存在于各 adapter 文件内部。
    """
    import re
    import app.routers.groupbuy as router_mod
    import app.services.groupbuy.platform as platform_mod
    import app.services.groupbuy.verify as verify_mod
    import app.services.groupbuy.store as store_mod

    pattern = re.compile(r'(if|elif|while)\s+.*platform(_code)?\s*==')
    for mod in (router_mod, platform_mod, verify_mod, store_mod):
        for line in inspect.getsource(mod).splitlines():
            stripped = line.strip()
            # 注释/文档字符串里会引用这条规则本身，不算真的分支
            if stripped.startswith("#"):
                continue
            m = pattern.search(line)
            if m and "不出现" in line:      # 说明性文案
                continue
            assert not m, "业务代码出现平台分支: %s: %s" % (mod.__name__, stripped)


def test_adapter_routing(gb):
    """平台编码 -> adapter 实例的映射正确、骨架与完整实现各归其位。"""
    from app.services.groupbuy.platform import get_adapter
    a = get_adapter("self")
    assert a.platform_code == "self"
    assert type(a).__name__ == "SelfPlatformAdapter"

    m = get_adapter("meituan")
    assert m.platform_code == "meituan"
    assert type(m).__name__ == "MeituanAdapter"
