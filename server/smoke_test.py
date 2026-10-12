#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""端到端冒烟测试：登录 → 商品 → 开台 → 下单 → 结账 → 交班 → 报表 → 参考系统兼容层。

覆盖新契约 /api/v1/* 与旧客户端兼容层 /saas/*、/pos/*、/store/*、/open/*，
共 80+ 项断言，包含格式（二次编码）、幂等、多租户隔离三类硬性验收点。
"""
import json
import os
import sqlite3
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8000").rstrip("/")
DB_FILE = os.getenv("DSH_POS_DATA",
                    os.path.join(os.path.dirname(os.path.abspath(__file__)), "data"))
DB_FILE = os.path.join(DB_FILE, "pos.db")
TOKEN = None
HDR = {}

# 打印策略的默认值直接取服务端源码，避免这里硬编码一份后与服务端漂移。
# 这两个常量还用于「写完还原」，让冒烟测试可以重复运行（见第 27 节）。
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    from app.routers.legacy_print import DEFAULT_PRINT_STRATEGY
    DEFAULT_STRATEGY_NAME = DEFAULT_PRINT_STRATEGY["strategyName"]
    # strategyType 不在默认策略里，由 /strategy/list 的存储层补默认值 2
    DEFAULT_STRATEGY_TYPE = 2
except Exception:  # pragma: no cover - 服务端源码不可用时的兜底
    DEFAULT_STRATEGY_NAME = "后厨档口"
    DEFAULT_STRATEGY_TYPE = 2


def ensure_server():
    """BASE 上没服务时就地起一个（CI 里只有 seed + 本脚本，没人先跑 run.py）。

    本地调试仍然优先连已启动的服务端 —— 只有连不上才自起，避免端口冲突。
    自起时把 OSS 基址对齐到 BASE，否则 catalog/syncUrl 会给出别的端口。
    """
    try:
        urllib.request.urlopen(BASE + "/health", timeout=2).read()
        return None
    except Exception:
        pass

    host = BASE.split("//", 1)[-1].split("/")[0]
    if not host.startswith(("127.0.0.1", "localhost", "[::1]")):
        raise SystemExit("连不上 %s，且不是本机地址，无法自动启动服务端" % BASE)
    port = int(host.rsplit(":", 1)[1]) if ":" in host else 80

    os.environ.setdefault("DSH_POS_OSS_BASE", BASE + "/oss/")
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

    import threading

    import uvicorn

    from app.main import app

    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=port,
                                           log_level="warning"))
    threading.Thread(target=server.run, daemon=True).start()
    for _ in range(150):
        try:
            urllib.request.urlopen(BASE + "/health", timeout=2).read()
            print("（未检测到已启动的服务端，已在 %s 就地拉起）" % BASE)
            return server
        except Exception:
            time.sleep(0.1)
    raise SystemExit("服务端启动失败：%s" % BASE)


def call(path, body=None, method="POST", raw=False, headers=None):
    url = BASE + path
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    for k, v in HDR.items():
        req.add_header(k, v)
    for k, v in (headers or {}).items():
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


def payload_of(r):
    """格式二：data 是二次编码的 JSON 字符串，这里替客户端解码一次。"""
    d = r.get("data")
    if isinstance(d, str):
        try:
            return json.loads(d)
        except Exception:
            return None
    return d


def sql_rows(sql, args=()):
    """只读校验落库结果（幂等 / 隔离测试用）。"""
    con = sqlite3.connect(DB_FILE, timeout=5)
    try:
        return con.execute(sql, args).fetchall()
    finally:
        con.close()


def sql_exec(sql, args=()):
    con = sqlite3.connect(DB_FILE, timeout=5)
    try:
        con.execute(sql, args)
        con.commit()
    finally:
        con.close()


PASS, FAIL = [], []


def check(name, cond, detail=""):
    (PASS if cond else FAIL).append(name)
    print(("  [OK]   " if cond else "  [FAIL] ") + name + ("  " + str(detail)[:120] if detail else ""))


def main():
    global TOKEN, HDR

    ensure_server()

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
    d = payload_of(r) or {}
    check("legacy /saas/login code=200", r.get("code") == 200, r.get("code"))
    check("legacy login 用格式一（data 是对象）", isinstance(r.get("data"), dict), type(r.get("data")).__name__)
    check("legacy loginToken 长度 >= 32", len(d.get("loginToken") or "") >= 32,
          len(d.get("loginToken") or ""))
    check("legacy user_info.storeId", str((d.get("user_info") or {}).get("storeId")) == "200001",
          (d.get("user_info") or {}).get("storeId"))
    check("legacy activationStatus=1", d.get("activationStatus") == 1)
    r = call("/pos/api/incr/productData", {"currVersion": "0", "downloadDataType": "CyProduct"})
    inc0 = ((payload_of(r) or {}).get("incrDataResult") or [{}])[0]
    check("legacy 增量同步有增量返回 03", inc0.get("resultCode") == "03", inc0)
    check("legacy 增量给出 incrFileName",
          inc0.get("incrFileName") == "CyProduct_0_%s.csv" % inc0.get("latestVersion"),
          inc0.get("incrFileName"))
    r = call("/pos/api/incr/productData",
             {"currVersion": inc0.get("latestVersion"), "downloadDataType": "CyProduct"})
    check("legacy 已是最新返回 01",
          ((payload_of(r) or {}).get("incrDataResult") or [{}])[0].get("resultCode") == "01")
    r = call("/some/unimplemented/api", {})
    check("legacy 兜底返回成功", r.get("code") == 200 and r.get("success") is True, r.get("code"))

    # ================================================================
    #  19 ~ 29：参考系统客户端兼容层全量回归
    # ================================================================

    print("\n19. 兼容层响应格式（二次编码）")
    r = call("/pos/area/list", {})
    check("area/list data 是二次编码字符串", isinstance(r.get("data"), str),
          type(r.get("data")).__name__)
    check("area/list 带 trace_no/success/key_sign",
          bool(r.get("trace_no")) and r.get("success") is True and r.get("key_sign") == "",
          {k: r.get(k) for k in ("trace_no", "success", "key_sign")})
    areas = payload_of(r) or {}
    check("二次解码得到 areaList", isinstance(areas.get("areaList"), list), areas)
    a0 = (areas.get("areaList") or [{}])[0]
    check("areaList 元素字段齐备",
          {"areaName", "id", "sort", "deskCount"} <= set(a0), a0)
    r = call("/store/getStoreConfig/", {})
    check("getStoreConfig 用格式一（data 是对象）",
          isinstance(r.get("data"), dict) and r.get("code") == 200, r)
    check("getStoreConfig 三个开关字段",
          {"openCashboxControl", "refundVerifyStatus", "openMemberPayStat"}
          <= set(r.get("data") or {}), r.get("data"))
    r = call("/saas/refreshJwt", None, "GET")
    check("refreshJwt 用格式一（无 trace_no/success）",
          isinstance(r.get("data"), str) and "trace_no" not in r, sorted(r))

    print("\n20. 兼容层门店 / 区域 / 桌台")
    r = call("/pos/store/info", {})
    si = payload_of(r) or {}
    check("store/info storeId=200001", str(si.get("storeId")) == "200001", si)
    check("store/info 含 storeMode/scanSwitch",
          si.get("storeMode") in (1, 2, 3) and "scanSwitch" in si, si)
    check("区域数 >= 3", len(areas.get("areaList") or []) >= 3,
          len(areas.get("areaList") or []))
    r = call("/pos/desk/list", {})
    desks = payload_of(r) or []
    check("desk/list 元素字段齐备",
          bool(desks) and {"deskId", "deskName", "deskStatus", "areaId", "seatsNum"}
          <= set(desks[0]), desks[:1])
    check("desk/list >= 8 张台", len(desks) >= 8, len(desks))
    check("desk/list 初始 deskStatus=1（空闲）",
          all(x.get("deskStatus") == 1 for x in desks), [x.get("deskStatus") for x in desks])
    r = call("/pos/surcharge/query", {})
    check("surcharge/query 返回空列表", payload_of(r) == [], r.get("data"))
    r = call("/legacy/_paths", None, "GET")
    pth = payload_of(r) or {}
    check("_paths 给出 tenantMd5/storeMd5",
          bool(pth.get("tenantMd5")) and bool(pth.get("storeMd5")), pth)

    print("\n21. 操作原因字典")
    r = call("/pos/operate/getOperatelist", {})
    ops = payload_of(r) or []
    check("getOperatelist 非空", len(ops) >= 20, len(ops))
    check("含 type 1..13 全部类型",
          {x.get("type") for x in ops} >= set(range(1, 14)),
          sorted({x.get("type") for x in ops}))
    check("元素字段 description/id/sort/type",
          bool(ops) and {"description", "id", "sort", "type"} <= set(ops[0]), ops[:1])
    check("退菜原因（type=5）含「有异物」",
          any(x.get("description") == "有异物" for x in ops if x.get("type") == 5),
          [x.get("description") for x in ops if x.get("type") == 5][:3])

    print("\n22. 兼容层开班")
    r = call("/pos/shift/open", {"cashOpen": 300.0})
    sh = payload_of(r) or {}
    shift_id = sh.get("shiftId")
    check("shift/open 返回 shiftId", bool(shift_id), sh)
    check("shift/open shiftNo 形如 S000001", (sh.get("shiftNo") or "").startswith("S"),
          sh.get("shiftNo"))
    check("shift/open 带回 cashOpen=300", float(sh.get("cashOpen") or 0) == 300.0, sh.get("cashOpen"))
    r = call("/pos/shift/open", {"cashOpen": 999.0})
    check("shift/open 幂等（返回同一班次）",
          (payload_of(r) or {}).get("shiftId") == shift_id, (payload_of(r) or {}).get("shiftId"))

    desk_ids = [int(x["deskId"]) for x in desks]
    d_a, d_b, d_c, d_d = desk_ids[1], desk_ids[2], desk_ids[3], desk_ids[4]

    print("\n23. 兼容层订单全流程")
    r = call("/pos/desk/openDesk", {"deskId": d_a, "customerNum": 3})
    od = payload_of(r) or {}
    desk0 = od.get("orderDesk") or {}
    order_a = desk0.get("orderNo")
    check("openDesk 建单并返回 orderNo", bool(order_a), desk0)
    check("openDesk deskStatus=2（占用）", desk0.get("deskStatus") == 2, desk0.get("deskStatus"))
    check("openDesk 返回 surcharges", "surcharges" in od, list(od))
    r = call("/pos/desk/openDesk", {"deskId": d_a, "customerNum": 3})
    check("openDesk 幂等（同台返回同一单）",
          ((payload_of(r) or {}).get("orderDesk") or {}).get("orderNo") == order_a)
    r = call("/pos/desk/list", {})
    dl = [x for x in (payload_of(r) or []) if int(x.get("deskId")) == d_a]
    check("desk/list 显示占用", bool(dl) and dl[0].get("deskStatus") == 2, dl[:1])

    prods_a = [
        {"productId": 1001, "productName": "宫保鸡丁", "prodPrice": 38.0, "prodUnitAmt": 38.0,
         "quantity": 2, "prodSalesAmt": 76.0, "unitName": "份"},
        {"productId": 1006, "productName": "米饭", "prodPrice": 2.0, "prodUnitAmt": 2.0,
         "quantity": 3, "prodSalesAmt": 6.0, "unitName": "碗"},
    ]
    r = call("/pos/order/submit", {"orderNo": order_a, "deskId": d_a, "orderType": 1,
                                   "customerNum": 3, "products": prods_a})
    det = payload_of(r) or {}
    check("submit 金额 38*2+2*3=82", float(det.get("amt") or 0) == 82.0, det.get("amt"))
    check("submit 明细 2 条", len(det.get("products") or []) == 2, det.get("products"))
    check("submit 记录用餐人数 3", int(det.get("numberOfDiners") or 0) == 3,
          det.get("numberOfDiners"))
    check("submit 返回 orderStatus=1（未结账）", det.get("orderStatus") == 1, det.get("orderStatus"))
    p0 = (det.get("products") or [{}])[0]
    check("明细字段名照抄 OrderProduct",
          {"productSaleNo", "productName", "quantity", "prodPrice", "prodSalesAmt", "prodPaidAmt"}
          <= set(p0), sorted(p0)[:8])
    r = call("/pos/order/detail", {"orderNo": order_a})
    check("order/detail 可回读", (payload_of(r) or {}).get("orderNo") == order_a)
    r = call("/pos/order/preSettle", {"orderNo": order_a})
    ps = payload_of(r) or {}
    check("preSettle 应付 82", float(ps.get("userReceiveAmt") or 0) == 82.0, ps.get("userReceiveAmt"))
    r = call("/pos/order/settle", {"orderNo": order_a,
                                   "payFlows": [{"pmSign": "2", "paymentAmt": 50.0}]})
    check("支付不足 -> 409", r.get("code") == 409, r.get("msg"))
    r = call("/pos/order/settle", {"orderNo": order_a,
                                   "payFlows": [{"pmSign": "2", "paymentAmt": 82.0}]})
    st = (payload_of(r) or {}).get("orderDetail") or {}
    check("settle 返回 orderDetail.orderStatus=2", st.get("orderStatus") == 2, st.get("orderStatus"))
    check("settle paymentType=2（现金）", st.get("paymentType") == 2, st.get("paymentType"))
    check("settle 落一条 payFlow",
          len(st.get("payFlows") or []) == 1 and (st.get("payFlows") or [{}])[0].get("pmSign") == "2",
          st.get("payFlows"))
    r = call("/pos/desk/list", {})
    dl = [x for x in (payload_of(r) or []) if int(x.get("deskId")) == d_a]
    check("结账后桌台自动释放", bool(dl) and dl[0].get("deskStatus") == 1, dl[:1])
    r = call("/pos/order/settle", {"orderNo": order_a,
                                   "payFlows": [{"pmSign": "2", "paymentAmt": 82.0}]})
    check("重复结账 -> 409 + 原单据",
          r.get("code") == 409 and ((payload_of(r) or {}).get("orderDetail") or {}).get("orderNo")
          == order_a, r.get("msg"))
    r = call("/pos/order/reverseSettle", {"orderNo": order_a})
    check("reverseSettle -> orderStatus=1",
          (payload_of(r) or {}).get("orderStatus") == 1, (payload_of(r) or {}).get("orderStatus"))
    r = call("/pos/order/settle", {"orderNo": order_a, "memberNo": "13800000001",
                                   "payFlows": [{"pmSign": "3", "paymentAmt": 82.0}]})
    st = (payload_of(r) or {}).get("orderDetail") or {}
    check("会员支付 -> orderStatus=2", st.get("orderStatus") == 2, st.get("orderStatus"))
    check("会员支付 paymentType=3", st.get("paymentType") == 3, st.get("paymentType"))

    print("\n24. 幂等：同一 clientOrderNo 提交 10 次")
    cno_leg = "LEGACY-" + uuid.uuid4().hex[:12]
    body = {"clientOrderNo": cno_leg, "orderType": 2,
            "products": [{"productId": 1003, "productName": "麻婆豆腐", "prodPrice": 26.0,
                          "prodUnitAmt": 26.0, "quantity": 1, "prodSalesAmt": 26.0}]}
    codes, nos = [], []
    for _ in range(10):
        r = call("/pos/order/submit", body)
        codes.append(r.get("code"))
        nos.append((payload_of(r) or {}).get("orderNo"))
    check("第 1 次 200、后 9 次 409", codes == [200] + [409] * 9, codes)
    check("10 次返回同一个 orderNo", len(set(nos)) == 1 and bool(nos[0]), set(nos))
    rows = sql_rows("select count(*) from t_order where client_order_no = ?", (cno_leg,))
    check("数据库里只有 1 条单据", rows and rows[0][0] == 1, rows)
    dup = sql_rows("select client_order_no, count(*) c from t_order "
                   "group by client_order_no having c > 1")
    check("全表无重复 client_order_no", dup == [], dup)

    print("\n25. 换台 / 清台 / 撤单 / 退单")
    r = call("/pos/desk/openDesk", {"deskId": d_b, "customerNum": 2})
    order_b = ((payload_of(r) or {}).get("orderDesk") or {}).get("orderNo")
    r = call("/pos/desk/transferDesk", {"orderNo": order_b, "fromDeskId": d_b, "toDeskId": d_c})
    td = payload_of(r) or {}
    check("transferDesk 返回 returnCode=1", td.get("returnCode") == 1, td.get("returnCode"))
    check("transferDesk 目标台已接管",
          int(((td.get("orderDesks") or [{}])[0] or {}).get("deskId") or 0) == d_c, td.get("orderDesks"))
    r = call("/pos/desk/list", {})
    dl = {int(x["deskId"]): x.get("deskStatus") for x in (payload_of(r) or [])}
    check("换台后原台空闲、目标台占用",
          dl.get(d_b) == 1 and dl.get(d_c) == 2, {d_b: dl.get(d_b), d_c: dl.get(d_c)})
    r = call("/pos/desk/openDesk", {"deskId": d_b, "customerNum": 2})
    order_b2 = ((payload_of(r) or {}).get("orderDesk") or {}).get("orderNo")
    check("原台释放后可再开台（新单号）", bool(order_b2) and order_b2 != order_b, order_b2)
    r = call("/pos/desk/transferDesk", {"orderNo": order_b2, "fromDeskId": d_b, "toDeskId": d_c})
    check("换到已占用台 -> 409", r.get("code") == 409, r.get("msg"))
    r = call("/pos/desk/clearDesk", {"deskId": d_b})
    check("未结账直接清台 -> 该单作废",
          ((payload_of(r) or {}).get("order") or {}).get("orderStatus") == 4,
          (payload_of(r) or {}).get("order"))
    r = call("/pos/desk/openDesk", {"deskId": d_d, "customerNum": 2})
    order_d = ((payload_of(r) or {}).get("orderDesk") or {}).get("orderNo")
    r = call("/pos/order/cancel", {"orderNo": order_d})
    check("撤单 -> orderStatus=4", (payload_of(r) or {}).get("orderStatus") == 4,
          (payload_of(r) or {}).get("orderStatus"))
    r = call("/pos/order/cancel", {"orderNo": order_d})
    check("重复撤单 -> 409", r.get("code") == 409, r.get("msg"))
    r = call("/pos/desk/clearDesk", {"deskId": d_d})
    check("clearDesk -> deskStatus=1",
          ((payload_of(r) or {}).get("orderDesks") or [{}])[0].get("deskStatus") == 1,
          payload_of(r))
    r = call("/pos/order/submit", {"orderNo": order_b, "deskId": d_c, "orderType": 1,
                                   "products": [{"productId": 1004, "productName": "水煮牛肉",
                                                 "prodPrice": 50.0, "prodUnitAmt": 50.0,
                                                 "quantity": 1, "prodSalesAmt": 50.0}]})
    check("改单后金额 50", float((payload_of(r) or {}).get("amt") or 0) == 50.0,
          (payload_of(r) or {}).get("amt"))
    r = call("/pos/order/settle", {"orderNo": order_b,
                                   "payFlows": [{"pmSign": "2", "paymentAmt": 50.0}]})
    check("order_b 结账成功",
          ((payload_of(r) or {}).get("orderDetail") or {}).get("orderStatus") == 2)
    r = call("/pos/order/refund", {"orderNo": order_b, "refundAmt": 50.0})
    check("退单 -> orderStatus=3", (payload_of(r) or {}).get("orderStatus") == 3,
          (payload_of(r) or {}).get("orderStatus"))
    r = call("/pos/order/refund", {"orderNo": order_b})
    check("重复退单 -> 409", r.get("code") == 409, r.get("msg"))

    print("\n26. 兼容层订单分页 / 交班")
    r = call("/pos/order/page", {"pageNum": 1, "pageSize": 5})
    pg = payload_of(r) or {}
    check("order/page 结构完整",
          {"total", "pages", "lastPage", "pageNum", "pageSize", "records"} <= set(pg),
          sorted(pg))
    check("order/page 有记录", int(pg.get("total") or 0) >= 4, pg.get("total"))
    check("order/page 记录是 OrderDetail",
          bool(pg.get("records")) and "orderStatus" in (pg.get("records") or [{}])[0])
    r = call("/pos/shift/close", {"shiftId": shift_id, "cashActual": 300.0})
    cs = payload_of(r) or {}
    check("shift/close status=1", cs.get("status") == 1, cs.get("status"))
    check("shift/close 已结单数 = 1", int(cs.get("settledCount") or 0) == 1, cs.get("settledCount"))
    check("shift/close 会员收款 = 82", float(cs.get("memberAmount") or 0) == 82.0,
          cs.get("memberAmount"))
    check("shift/close 退款 = 50", float(cs.get("refundAmount") or 0) == 50.0,
          cs.get("refundAmount"))
    # order_b 收现金 50 又整单退掉，净额 0；钱箱只剩备用金。
    check("shift/close 现金收款 = 0（整单退的不算）",
          float(cs.get("cashAmount") or 0) == 0.0, cs.get("cashAmount"))
    check("shift/close 应有现金 = 300", float(cs.get("cashShould") or 0) == 300.0,
          cs.get("cashShould"))
    r = call("/pos/shift/close", {"shiftId": shift_id})
    check("重复交班 -> 409 + 原班次单据",
          r.get("code") == 409 and (payload_of(r) or {}).get("shiftId") == shift_id,
          r.get("msg"))
    r = call("/pos/handover/getHandoverList", {"pageNum": 1, "pageSize": 5})
    hl = payload_of(r) or {}
    check("getHandoverList 有交接记录", int(hl.get("total") or 0) >= 1, hl.get("total"))
    check("交接记录字段齐备",
          bool(hl.get("records")) and {"handoverNo", "handoverTime", "terminalNo"}
          <= set((hl.get("records") or [{}])[0]), (hl.get("records") or [{}])[0])
    r = call("/pos/upload/shiftticketV1", {"shiftNo": "S000001", "content": "x"})
    check("shiftticketV1 无 data 字段", "data" not in r, sorted(r))
    check("shiftticketV1 success=true", r.get("success") is True, r)

    print("\n27. 打印模板 / 设备 / 策略")
    r = call("/pos/print/sync/template", {})
    tp = payload_of(r) or {}
    check("sync/template 四个顶层键",
          {"tagTemplateList", "receiptTemplateList", "storeTemplateConfigList",
           "receiptOrderTemplateList"} <= set(tp), sorted(tp))
    check("标签模板 1 条", len(tp.get("tagTemplateList") or []) == 1)
    check("小票模板 10 条", len(tp.get("receiptTemplateList") or []) == 10,
          len(tp.get("receiptTemplateList") or []))
    check("小票模板含 templateName/templateType",
          {"templateName", "templateType", "templateId"}
          <= set((tp.get("receiptTemplateList") or [{}])[0]))
    check("订单小票模板 10 条且含欢迎语",
          len(tp.get("receiptOrderTemplateList") or []) == 10
          and "welcomeMessage" in (tp.get("receiptOrderTemplateList") or [{}])[0])
    r = call("/pos/print/sync/device", {})
    dv = payload_of(r) or {}
    check("sync/device 两个顶层键",
          {"printStrategyList", "deviceList"} == set(dv), sorted(dv))
    strat = (dv.get("printStrategyList") or [{}])[0]
    check("打印策略含 strategyName/strategyType",
          strat.get("strategyName") == DEFAULT_STRATEGY_NAME
          and strat.get("strategyType") == DEFAULT_STRATEGY_TYPE,
          strat.get("strategyName"))
    check("打印策略不含 deviceList（与抓包一致）", "deviceList" not in strat)
    check("内置打印机已绑定",
          len(dv.get("deviceList") or []) == 1
          and (dv.get("deviceList") or [{}])[0].get("deviceId") == "LOCAL001")
    r = call("/pos/print/strategy/list", {})
    sl = payload_of(r) or []
    check("strategy/list 非空", bool(sl), sl)
    check("strategy/list 字段子集正确",
          set(sl[0]) == {"defaultStrategySwitch", "strategyName", "deviceList", "strategyId",
                         "modifierPrintMode", "bilTypeList", "strategyType"}, sorted(sl[0]))
    check("strategy/list 带 deviceList 与 bilTypeList",
          len(sl[0].get("deviceList") or []) == 1 and sl[0].get("bilTypeList") == [3, 4, 13])
    r = call("/pos/print/strategy/list", {"strategyType": 99})
    check("strategyType 过滤生效（无匹配返回空）", payload_of(r) == [], r.get("data"))
    r = call("/pos/print/strategy/info", {})
    inf = payload_of(r) or {}
    check("strategy/info 13 个字段",
          {"foods", "addFoodNum", "strategyType", "defaultStrategySwitch", "deskIds",
           "orderTypeSwitch", "refundFoodNum", "orderSourceSwitch", "addFoodSwitch",
           "strategyName", "changeDeskSwitch", "refundFoodSwitch", "singleFoodSwitch"}
          == set(inf), sorted(inf))
    check("strategy/info foods 是列表（非 null）", isinstance(inf.get("foods"), list), inf.get("foods"))
    r = call("/pos/print/strategy/save", {"strategyName": "冒烟测试档口", "addFoodNum": 3,
                                          "strategyId": sl[0].get("strategyId")})
    check("strategy/save 回 strategyId",
          (payload_of(r) or {}).get("strategyId") == sl[0].get("strategyId"), payload_of(r))
    r = call("/pos/print/strategy/info", {})
    check("strategy/save 后 info 可读回",
          (payload_of(r) or {}).get("strategyName") == "冒烟测试档口", payload_of(r))
    r = call("/pos/print/sync/device", {})
    check("strategy/save 后 device 快照同步",
          (((payload_of(r) or {}).get("printStrategyList") or [{}])[0]).get("strategyName")
          == "冒烟测试档口")

    # strategy/save 是持久化写入（kv_set），不还原的话第 570 行「期望默认值」的断言
    # 在第二次运行时必然失败 —— 测试污染自己。存完立刻恢复默认策略名。
    ORIG_STRATEGY_NAME = (payload_of(r) or {}).get("strategyName")
    call("/pos/print/strategy/save", {"strategyName": DEFAULT_STRATEGY_NAME,
                                      "addFoodNum": 0})
    r = call("/pos/print/strategy/info", {})
    check("strategy/save 还原后恢复默认值（测试可重入）",
          (payload_of(r) or {}).get("strategyName") == DEFAULT_STRATEGY_NAME, payload_of(r))
    r = call("/open/print/device/brandList", {})
    bl = payload_of(r) or []
    check("brandList 5 个品牌", len(bl) == 5, len(bl))
    check("brandList 字段 brand_name/brand_id",
          bool(bl) and {"brand_name", "brand_id"} <= set(bl[0]), bl[:1])
    r = call("/open/print/device/list", {})
    check("device/list 返回内置打印机", len(payload_of(r) or []) == 1, payload_of(r))

    print("\n28. 功能开关 / 商户授权")
    r = call("/open/store/queryAbilityOpen", {})
    ab = payload_of(r) or {}
    check("queryAbilityOpen openStatus=1", ab.get("openStatus") == 1, ab)
    check("queryAbilityOpen 含 invoiceSwitch", "invoiceSwitch" in ab, sorted(ab))
    r = call("/open/merchant/queryMerchantConfig", {})
    mc = payload_of(r) or {}
    codes = [x.get("functionCode") for x in (mc.get("functionCodeInfoList") or [])]
    check("queryMerchantConfig 两条授权", codes == ["PTWM", "SFTG"], codes)
    check("授权含 expirationTime",
          all(x.get("expirationTime") for x in (mc.get("functionCodeInfoList") or [])))

    print("\n29. 多租户隔离")
    iso_tenant, iso_store, iso_area, iso_desk = 999002, 999002, 999002, 999002
    try:
        sql_exec("insert into tenant(id,code,name,edition,status) values(?,?,?,?,1)",
                 (iso_tenant, "ISO002", "隔离测试租户", "STD"))
        sql_exec("insert into store(id,tenant_id,code,name,store_mode,status) values(?,?,?,?,1,1)",
                 (iso_store, iso_tenant, "ISO002", "隔离测试门店"))
        sql_exec("insert into desk_area(id,store_id,name,sort) values(?,?,?,0)",
                 (iso_area, iso_store, "隔离区"))
        sql_exec("insert into desk(id,store_id,area_id,name,capacity,status,current_order_id) "
                 "values(?,?,?,?,4,0,0)", (iso_desk, iso_store, iso_area, "隔离测试台"))

        iso_hdr = {"X-Store-Id": str(iso_store), "X-Tenant-Id": str(iso_tenant)}
        r = call("/pos/desk/list", {}, headers=iso_hdr)
        iso_desks = payload_of(r) or []
        check("指定门店只看到自己的 1 张台",
              len(iso_desks) == 1 and iso_desks[0].get("deskName") == "隔离测试台",
              [x.get("deskName") for x in iso_desks])
        check("隔离门店的响应带自己的 storeId",
              bool(iso_desks) and iso_desks[0].get("storeId") == str(iso_store),
              iso_desks[:1])
        r = call("/pos/area/list", {}, headers=iso_hdr)
        iso_areas = (payload_of(r) or {}).get("areaList") or []
        check("指定门店只看到自己的 1 个区域", len(iso_areas) == 1, iso_areas)
        r = call("/pos/order/page", {}, headers=iso_hdr)
        check("隔离门店看不到别家订单",
              int((payload_of(r) or {}).get("total") or 0) == 0, (payload_of(r) or {}).get("total"))
        r = call("/pos/desk/list", {})
        main_desks = payload_of(r) or []
        check("默认门店看不到隔离门店的桌台",
              all(x.get("deskName") != "隔离测试台" for x in main_desks), len(main_desks))
        r = call("/pos/order/page", {})
        check("默认门店订单数与隔离门店无关",
              int((payload_of(r) or {}).get("total") or 0) >= 4, (payload_of(r) or {}).get("total"))
        r = call("/pos/operate/getOperatelist", {}, headers=iso_hdr)
        iso_ops = payload_of(r) or []
        check("操作原因字典按门店隔离",
              bool(iso_ops) and all(int(x.get("id") or 0) // 1000 == iso_store for x in iso_ops),
              iso_ops[:1])
    finally:
        for sql, arg in (("delete from desk where store_id=?", (iso_store,)),
                         ("delete from desk_area where store_id=?", (iso_store,)),
                         ("delete from store where id=?", (iso_store,)),
                         ("delete from tenant where id=?", (iso_tenant,))):
            try:
                sql_exec(sql, arg)
            except Exception:
                pass

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
