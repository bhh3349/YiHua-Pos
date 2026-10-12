"""并发幂等 —— 任务卡 4.4：同一张券并发核销，只能成功一次（资金安全）。

用线程池同时打 N 个核销请求，断言恰好 1 个 200、其余全是 COUPON_USED；
DB 里券状态为已核销、流水恰好一条 redeem。
"""
import concurrent.futures

import pytest

from app.services.groupbuy import guard
from conftest import post

GB = "/api/v1/groupbuy"
N = 8          # 并发线程数
ROUNDS = 5     # 跑几轮不同券


def _setup(client, auth_headers):
    r = post(client, GB + "/configSave",
             {"platformCode": "self", "storeIdList": [200001],
              "signKey": "k" * 20}, auth_headers).json()
    assert r["code"] == "200", r


@pytest.fixture()
def gb(client, auth_headers):
    guard.reset_rate_limit()      # 并发用例本身就是高频请求，先清零再放大窗口额度
    _setup(client, auth_headers)
    return client, auth_headers


def _issue(client, auth_headers, code):
    r = post(client, GB + "/coupon/issue",
             {"couponCode": code, "discountAmount": "50.00",
              "faceValue": "50.00"}, auth_headers).json()
    assert r["code"] == "200", r


def _redeem(client, auth_headers, code):
    return post(client, GB + "/coupon/redeem",
                {"platformCode": "self", "couponCode": code,
                 "storeId": 200001, "orderId": 0}, auth_headers).json()


def test_concurrent_redeem_exactly_one_success(gb):
    client, h = gb
    for r in range(ROUNDS):
        guard.reset_rate_limit()      # 每轮清零，避免自动限流干扰并发断言
        code = "CONC%03d%d" % (100 + r, r)
        _issue(client, h, code)

        with concurrent.futures.ThreadPoolExecutor(max_workers=N) as pool:
            futures = [pool.submit(_redeem, client, h, code) for _ in range(N)]
            bodies = [f.result() for f in futures]

        ok = [b for b in bodies if b["code"] == "200"]
        dup = [b for b in bodies if b["code"] == "COUPON_USED"]
        assert len(ok) == 1, (code, bodies)
        assert len(dup) == N - 1, (code, bodies)

        # 落库终态：一条 redeem 流水，抵扣金额只记一次
        lg = post(client, GB + "/redeemLog", {"couponCode": code}, h).json()
        assert lg["code"] == "200", lg
        redeems = [x for x in lg["data"]["list"] if x["action"] == "redeem"]
        assert len(redeems) == 1, lg["data"]["list"]
        assert redeems[0]["actualDiscount"] == "50.00"
