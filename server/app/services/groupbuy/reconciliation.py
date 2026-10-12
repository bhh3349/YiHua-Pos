"""对账 —— 本地核销记录 vs 平台侧核销记录，算差异、导 CSV。

任务卡 4.5：
  平台说今天在你店核销了 50 张券抵 ¥4,950，我们记录 48 张 ¥4,720，
  差异 2 张 ¥230 —— 这个差额必须能算出来、能导出。

**口径**（两侧必须一致，否则天天报假差异）：
  * 已撤销的核销**两侧都不计**：本地把流水标记为 REVERSED 后剔除，
    自有平台的 :meth:`SelfPlatformAdapter.reconcile` 同样只回未作废的流水。
  * 匹配键 = ``(platform_code, coupon_code)``：一张券当天只该被核销一次。
  * 全程 Decimal 累加，最后才落地成字符串。

差异分类：
  ============  ==========================================
  MATCH         两边都有、金额一致
  AMOUNT_DIFF   两边都有、金额不一致
  LOCAL_ONLY    本地有、平台没有（平台少记 / 我们多记）
  PLATFORM_ONLY 平台有、本地没有（漏单 —— 最该查的一种）
  ============  ==========================================
"""
import csv
import os
from datetime import datetime
from decimal import Decimal
from typing import Dict, List, Optional

from sqlalchemy.orm import Session

from ...models_groupbuy import GroupbuyRedeemLog
from .crypto import mask_secret
from .errors import GroupbuyError, PlatformErrorCode
from .platform import CouponQuery, PlatformRedeemRecord, get_adapter

ZERO = Decimal("0.00")

STATUS_MATCH = "MATCH"
STATUS_AMOUNT_DIFF = "AMOUNT_DIFF"
STATUS_LOCAL_ONLY = "LOCAL_ONLY"
STATUS_PLATFORM_ONLY = "PLATFORM_ONLY"

_STATUS_NAMES = {
    STATUS_MATCH: "一致",
    STATUS_AMOUNT_DIFF: "金额不符",
    STATUS_LOCAL_ONLY: "本地多记",
    STATUS_PLATFORM_ONLY: "平台多记",
}


class ReconcileItem(dict):
    """一条比对结果。继承 dict 便于直接进 JSON 响应 / CSV 行。"""


def _local_records(db: Session, tenant_id: int, platform_code: str,
                   store_id: Optional[int], day: datetime) -> Dict[str, dict]:
    """本地未作废的核销流水，键为券码。"""
    from .self_platform import REVERSED

    start = day.replace(hour=0, minute=0, second=0, microsecond=0)
    end = start.replace(hour=23, minute=59, second=59)
    q = (db.query(GroupbuyRedeemLog)
           .filter(GroupbuyRedeemLog.tenant_id == tenant_id,
                   GroupbuyRedeemLog.platform_code == platform_code,
                   GroupbuyRedeemLog.action == "redeem",
                   GroupbuyRedeemLog.reason != REVERSED,
                   GroupbuyRedeemLog.created_at >= start,
                   GroupbuyRedeemLog.created_at <= end))
    if store_id:
        q = q.filter(GroupbuyRedeemLog.store_id == store_id)

    out: Dict[str, dict] = {}
    for r in q.all():
        # 同一券码本地出现多条未作废核销 —— 这本身就是异常，累加起来
        # 让差异体现出来，而不是静默取一条把问题藏掉。
        prev = out.get(r.coupon_code)
        amount = Decimal(r.actual_discount or 0)
        if prev is None:
            out[r.coupon_code] = {"couponCode": r.coupon_code,
                                  "discountAmount": amount,
                                  "orderNo": str(r.order_id or ""),
                                  "redeemTime": r.created_at}
        else:
            prev["discountAmount"] += amount
    return out


def _platform_records(req: CouponQuery, day: datetime) -> Dict[str, dict]:
    """平台侧记录。adapter 报错时换成对账专用错误，不裸抛 500。"""
    adapter = get_adapter(req.platform_code)
    try:
        rows: List[PlatformRedeemRecord] = adapter.reconcile(req, day)
    except GroupbuyError:
        raise
    except Exception as e:            # 远端平台报错：只带异常类型，不带任何可能含密钥的信息
        raise GroupbuyError(PlatformErrorCode.ADAPTER_ERROR,
                            "拉取平台对账数据失败: %s" % type(e).__name__)
    out: Dict[str, dict] = {}
    for r in rows:
        prev = out.get(r.coupon_code)
        if prev is None:
            out[r.coupon_code] = {"couponCode": r.coupon_code,
                                  "discountAmount": Decimal(r.discount_amount or 0),
                                  "orderNo": r.order_no or "",
                                  "redeemTime": r.redeem_time}
        else:
            prev["discountAmount"] += Decimal(r.discount_amount or 0)
    return out


def reconcile(db: Session, req: CouponQuery, day: datetime,
              store_id: Optional[int] = None) -> dict:
    """按日/按平台对账，返回汇总 + 逐条差异明细。"""
    local = _local_records(db, req.tenant_id, req.platform_code, store_id, day)
    platform = _platform_records(req, day)

    items: List[ReconcileItem] = []
    local_total = ZERO
    platform_total = ZERO
    diff_count = 0
    diff_amount = ZERO

    keys = sorted(set(local) | set(platform))
    for code in keys:
        l = local.get(code)
        p = platform.get(code)
        if l is not None:
            local_total += l["discountAmount"]
        if p is not None:
            platform_total += p["discountAmount"]

        if l is not None and p is not None:
            if l["discountAmount"] == p["discountAmount"]:
                status = STATUS_MATCH
            else:
                status = STATUS_AMOUNT_DIFF
                diff_count += 1
                diff_amount += abs(l["discountAmount"] - p["discountAmount"])
        elif l is not None:
            status = STATUS_LOCAL_ONLY
            diff_count += 1
            diff_amount += l["discountAmount"]
        else:
            status = STATUS_PLATFORM_ONLY
            diff_count += 1
            diff_amount += p["discountAmount"]

        items.append(ReconcileItem({
            "couponCode": code,
            "status": status,
            "statusName": _STATUS_NAMES[status],
            "localAmount": str(l["discountAmount"]) if l else None,
            "platformAmount": str(p["discountAmount"]) if p else None,
            "diffAmount": str(((l["discountAmount"] if l else ZERO)
                               - (p["discountAmount"] if p else ZERO)))
                          if (l or p) else "0.00",
            "localOrderNo": (l or {}).get("orderNo") or "",
            "platformOrderNo": (p or {}).get("orderNo") or "",
            "localTime": _fmt((l or {}).get("redeemTime")),
            "platformTime": _fmt((p or {}).get("redeemTime")),
        }))

    only_diff = [i for i in items if i["status"] != STATUS_MATCH]
    return {
        "date": day.strftime("%Y-%m-%d"),
        "platformCode": req.platform_code,
        "storeId": store_id or 0,
        "summary": {
            "localCount": len(local),
            "platformCount": len(platform),
            "matchCount": len(items) - len(only_diff),
            "diffCount": diff_count,
            "localAmount": str(local_total),
            "platformAmount": str(platform_total),
            "diffAmount": str(diff_amount),
            "amountGap": str(local_total - platform_total),
        },
        "items": items,
        "diffItems": only_diff,
    }


def _fmt(dt: Optional[datetime]) -> str:
    return dt.strftime("%Y-%m-%d %H:%M:%S") if dt else ""


CSV_HEADER = ["日期", "平台", "券码", "状态", "本地金额", "平台金额",
              "差异金额", "本地订单号", "平台订单号", "本地核销时间", "平台核销时间"]


def build_csv_rows(result: dict) -> List[list]:
    """对账结果 -> CSV 行（金额列保持字符串形态，Excel 里不会变成科学计数）。"""
    rows = []
    for i in result["items"]:
        rows.append([
            result["date"], result["platformCode"], i["couponCode"], i["statusName"],
            i["localAmount"] if i["localAmount"] is not None else "",
            i["platformAmount"] if i["platformAmount"] is not None else "",
            i["diffAmount"], i["localOrderNo"], i["platformOrderNo"],
            i["localTime"], i["platformTime"],
        ])
    return rows


def export_csv(result: dict, path: str) -> str:
    """导出对账 CSV，**UTF-8 with BOM**（Excel 打开中文不乱码）。"""
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(CSV_HEADER)
        for r in build_csv_rows(result):
            w.writerow(r)
    return path
