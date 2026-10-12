#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""端到端冒烟测试：登录 → 商品 → 开台 → 下单 → 结账 → 交班 → 报表。"""
import json
import sys
import urllib.error
import urllib.request
import uuid

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8000"
TOKEN = None
HDR = {}


def call(path, body=None, method="POST", raw=False):
    url = BASE + path
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    for k, v in HDR.items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            txt = r.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        txt = e.read().decode("utf-8")
    if raw:
        return txt
    try:
        return json.loads(txt)
    except Exception:
        return {"_raw": txt}


PASS, FAIL = [], []


def check(name, cond, detail=""):
    (PASS if cond else FAIL).append(name)
    print(("  [OK]   " if cond else "  [FAIL] ") + name + ("  " + str(detail)[:120] if detail else ""))


def main():
    global TOKEN, HDR

    print("=" * 64)
    print("1. 健康检查")
    r = call("/health", None, "GET")
    check("GET /health", r.get("ok") is True, r)

    print("\n2. 登录")
    r = call("/api/v1/auth/login", {"account": "admin", "password": "123456",
                                    "deviceId": "DEV-TEST-001", "terminalNo": "T0001"})
    check("auth/login 返回 200", r.get("code") == "200", r.get("message"))
    data = r.get("data") or {}
    TOKEN = data.get("token")
    check("拿到 token", bool(TOKEN))
    HDR = {"Authorization": "Bearer %s" % TOKEN, "X-Client": "win",
           "X-Terminal-No": "T0001", "X-Trace-No": uuid.uuid4().hex}
    check("userInfo 完整", bool(data.get("userInfo", {}).get("account")),
          data.get("userInfo"))
    check("storeInfo 完整", data.get("storeInfo", {}).get("storeId") == 200001,
          data.get("storeInfo"))

    print("\n3. 错误密码应被拒")
    r = call("/api/v1/auth/login", {"account": "admin", "password": "wrong"})
    check("错误密码 -> 401", r.get("code") == "401", r.get("message"))

    print("\n4. 无 token 访问应被拒")
    saved, HDR = HDR, {}
    r = call("/api/v1/catalog/page", {"pageSize": 5})
    check("无 token -> 401", r.get("code") == "401", r.get("message"))
    HDR = saved

    print("\n5. 开班（真实流程：先开班再收银）")
    r = call("/api/v1/shift/open", {"cashOpen": 500.0})
    check("开班", r.get("code") == "200", r.get("data"))

    print("\n5b. 商品")
    r = call("/api/v1/catalog/categoryList")
    check("分类列表非空", r.get("code") == "200" and len(r.get("data") or []) >= 5,
          len(r.get("data") or []))
    r = call("/api/v1/catalog/page", {"pageSize": 50})
    prods = (r.get("data") or {}).get("list") or []
    check("商品列表 >= 12", len(prods) >= 12, len(prods))
    r = call("/api/v1/catalog/page", {"keyword": "宫保"})
    check("关键字搜索命中", len((r.get("data") or {}).get("list") or []) == 1)
    r = call("/api/v1/catalog/version", {})
    check("版本查询", r.get("code") == "200", r.get("data"))
    r = call("/api/v1/catalog/syncUrl", {})
    urls = (r.get("data") or {}).get("urls") or []
    check("CSV 导出地址", len(urls) >= 3, urls[0] if urls else None)
    if urls:
        txt = call(urls[0]["url"].replace(BASE, ""), None, "GET", raw=True)
        check("CSV 可下载且含表头", "id," in txt.splitlines()[0], txt.splitlines()[0][:80])

    print("\n6. 桌台")
    r = call("/api/v1/desk/areaList", {})
    check("区域 >= 3", len(r.get("data") or []) >= 3)
    r = call("/api/v1/desk/list", {})
    desks = r.get("data") or []
    check("桌台 >= 8", len(desks) >= 8, len(desks))
    r = call("/api/v1/desk/open", {"deskId": 1, "guestCount": 3})
    check("开台 A01", r.get("code") == "200", r.get("data"))

    print("\n7. 下单")
    cno = "TEST-" + uuid.uuid4().hex[:12]
    items = [{"productId": 1001, "productName": "宫保鸡丁", "price": 38.0, "qty": 2},
             {"productId": 1006, "productName": "米饭", "price": 2.0, "qty": 3}]
    r = call("/api/v1/order/create", {"clientOrderNo": cno, "deskId": 1,
                                      "orderType": 1, "items": items})
    check("order/create", r.get("code") == "200", r.get("message"))
    o = r.get("data") or {}
    oid = o.get("orderId")
    check("金额计算 38*2+2*3=82", float(o.get("amountTotal") or 0) == 82.0, o.get("amountTotal"))
    check("明细 2 条", len(o.get("items") or []) == 2)

    print("\n8. 幂等：同一 clientOrderNo 再提交")
    r = call("/api/v1/order/create", {"clientOrderNo": cno, "deskId": 1, "items": items})
    check("重复提交 -> 409 且返回原单", r.get("code") == "409"
          and (r.get("data") or {}).get("orderId") == oid, r.get("code"))

    print("\n9. 折扣 + 预结")
    r = call("/api/v1/order/discount", {"orderId": oid, "target": "order",
                                        "type": "amount", "value": 2.0})
    check("整单减 2 元 -> 应付 80", float((r.get("data") or {}).get("amountPay") or 0) == 80.0,
          r.get("data"))
    r = call("/api/v1/order/preSettle", {"orderId": oid})
    check("预结应付 80", float((r.get("data") or {}).get("amountPay") or 0) == 80.0)

    print("\n10. 支付金额不足应被拒")
    r = call("/api/v1/order/settle", {"clientOrderNo": cno, "orderId": oid,
                                      "payments": [{"payType": "CASH", "amount": 50.0}]})
    check("金额不足 -> 409", r.get("code") == "409", r.get("message"))

    print("\n11. 结账（现金 100 找零 20）")
    r = call("/api/v1/order/settle", {"clientOrderNo": cno, "orderId": oid,
                                      "payments": [{"payType": "CASH", "amount": 100.0}]})
    d = r.get("data") or {}
    check("结账成功", r.get("code") == "200", r.get("message"))
    check("找零 20", float(d.get("change") or 0) == 20.0, d.get("change"))

    print("\n12. 结账后桌台应自动清台")
    r = call("/api/v1/desk/list", {})
    d1 = [x for x in (r.get("data") or []) if x["id"] == 1]
    check("A01 已释放", d1 and d1[0]["status"] == 0, d1[0]["status"] if d1 else None)

    print("\n13. 重复结账应被拒")
    r = call("/api/v1/order/settle", {"clientOrderNo": cno, "orderId": oid,
                                      "payments": [{"payType": "CASH", "amount": 100.0}]})
    check("重复结账 -> 409", r.get("code") == "409", r.get("message"))

    print("\n14. 会员余额支付")
    r = call("/api/v1/member/query", {"phone": "13800000001"})
    m = r.get("data") or {}
    check("会员查询", m.get("phone") == "13800000001", m)
    bal0 = float(m.get("balance") or 0)
    cno2 = "TEST-" + uuid.uuid4().hex[:12]
    r = call("/api/v1/order/create", {"clientOrderNo": cno2, "orderType": 2,
                                      "items": [{"productId": 1003, "productName": "麻婆豆腐",
                                                 "price": 26.0, "qty": 1}]})
    oid2 = (r.get("data") or {}).get("orderId")
    r = call("/api/v1/order/settle", {"clientOrderNo": cno2, "orderId": oid2,
                                      "payments": [{"payType": "MEMBER", "amount": 26.0,
                                                    "memberId": m.get("memberId")}]})
    check("会员支付成功", r.get("code") == "200", r.get("message"))
    r = call("/api/v1/member/query", {"memberId": m.get("memberId")})
    check("余额扣减 26", abs(float((r.get("data") or {}).get("balance") or 0) - (bal0 - 26)) < 0.01,
          "%s -> %s" % (bal0, (r.get("data") or {}).get("balance")))

    print("\n15. 充值")
    r = call("/api/v1/member/recharge", {"memberId": m.get("memberId"),
                                         "amount": 100.0, "giftAmount": 10.0})
    check("充值 100 送 10", float((r.get("data") or {}).get("balance") or 0)
          == round(bal0 - 26 + 110, 2), r.get("data"))

    print("\n16. 交班")
    r = call("/api/v1/shift/current", {})
    cur = r.get("data") or {}
    check("当前班次有数据", cur.get("shiftId") is not None, cur)
    check("班次现金额 = 100（现金单）", abs(float(cur.get("cashAmount") or 0) - 100.0) < 0.01,
          cur.get("cashAmount"))
    check("班次已结单数 = 2", int(cur.get("settledCount") or 0) == 2, cur.get("settledCount"))
    r = call("/api/v1/shift/close", {"cashActual": 600.0})
    check("交班", r.get("code") == "200", r.get("data"))
    d = r.get("data") or {}
    check("应有现金 = 500 + 100", abs(float(d.get("cashShould") or 0) - 600.0) < 0.01,
          d.get("cashShould"))

    print("\n17. 报表")
    r = call("/api/v1/report/summary", {"days": 7})
    s = r.get("data") or {}
    check("汇总已结账 >= 2 单", int(s.get("settledCount") or 0) >= 2, s)
    check("实收 >= 106", float(s.get("amountPay") or 0) >= 106.0, s.get("amountPay"))
    r = call("/api/v1/report/payType", {"days": 7})
    check("支付方式统计非空", len(r.get("data") or []) >= 2, r.get("data"))
    r = call("/api/v1/report/productRank", {"days": 7})
    check("商品排行非空", len(r.get("data") or []) >= 2)

    print("\n18. 参考系统客户端兼容层")
    r = call("/saas/login", {"userName": "admin", "password": "123456", "loginType": "1"})
    d = r.get("data") or {}
    check("legacy /saas/login code=200", r.get("code") == "200")
    check("legacy loginToken 长度 >= 32", len(d.get("loginToken") or "") >= 32,
          len(d.get("loginToken") or ""))
    check("legacy user_info.storeId", str((d.get("user_info") or {}).get("storeId")) == "200001",
          (d.get("user_info") or {}).get("storeId"))
    check("legacy activationStatus=1", d.get("activationStatus") == 1)
    r = call("/pos/api/incr/productData", {"currVersion": "0", "downloadDataType": "CyProduct"})
    check("legacy 增量同步返回 02",
          ((r.get("data") or {}).get("incrDataResult") or [{}])[0].get("resultCode") == "02")
    r = call("/some/unimplemented/api", {})
    check("legacy 兜底返回成功", r.get("code") == "200" and r.get("success") is True)

    print("\n" + "=" * 64)
    print("通过 %d 项，失败 %d 项" % (len(PASS), len(FAIL)))
    if FAIL:
        print("失败项：")
        for f in FAIL:
            print("   - " + f)
    print("=" * 64)
    return 0 if not FAIL else 1


if __name__ == "__main__":
    sys.exit(main())
