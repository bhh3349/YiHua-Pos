namespace YiHua.Pos.UI.Services.Report;

/// <summary>
/// 报表计算器（纯逻辑）。
///
/// 计算规则（明确并已测试）：
/// - 营业额：Σ(应付 - 已退)，作废单排除，冲正单（负数）计入
/// - 退菜：已退金额从营业额中减去
/// - 抹零：抹掉的部分计入折扣，不算营业额
/// - 客单价：营业额 / 订单数（分母用订单数，不用商品件数）
/// - 作废单：一律排除
/// - 跨天订单：按结账时间归属，无结账时间按 下单时间
/// - 取整：显式 MidpointRounding.AwayFromZero
/// - 占比：分母为 0 时返回 0，不抛异常、不返回 NaN
/// </summary>
public static class ReportCalculator
{
    /// <summary>
    /// 营业额：有效订单的 (应付 - 已退) 之和。
    /// 作废单排除；冲正单（负数应付）计入。
    /// </summary>
    /// <summary>
    /// 营业额：Σ (应付 - 已退) ，**只算已结账和已退单**。
    ///
    /// ⚠️ 未结账订单不算营业额。之前这里只排除作废单，
    /// 把还没收到钱的单也算进去了 —— 老板看到「今天做了 800」，
    /// 实际钱箱里只有 500，差额是两桌还没结的。
    /// 营业额是「已经收到的钱」，不是「开了多少单」。
    ///
    /// 冲正单（负数应付）计入，作废单排除。
    /// </summary>
    public static decimal CalcTurnover(IEnumerable<ReportOrderDto> orders)
    {
        decimal total = 0;
        foreach (var o in orders)
        {
            if (!IsRevenueOrder(o)) continue;
            total += o.AmountPay - o.AmountRefund;
        }
        return Round(total);
    }

    /// <summary>
    /// 是否计入营业额：已结账(1) 或 已退单(2)。
    /// 待结账(0) 和 作废(3) 都不算。
    /// </summary>
    public static bool IsRevenueOrder(ReportOrderDto o) => o.Status is 1 or 2;

    /// <summary>
    /// 客单价：营业额 / 有效订单数。分母用订单数。
    /// 无订单时返回 0。
    /// </summary>
    public static decimal CalcAvgTicket(decimal turnover, int orderCount)
    {
        if (orderCount <= 0) return 0m;
        return Round(turnover / orderCount);
    }

    /// <summary>
    /// 优惠合计：Σ 折扣额（作废单排除）。
    /// </summary>
    public static decimal CalcDiscountTotal(IEnumerable<ReportOrderDto> orders)
        => Round(orders.Where(o => !o.IsVoid).Sum(o => o.AmountDiscount));

    /// <summary>
    /// 实收合计：Σ 实收金额（作废单排除）。
    /// </summary>
    public static decimal CalcNetReceived(IEnumerable<ReportOrderDto> orders)
        => Round(orders.Where(o => !o.IsVoid).Sum(o => o.AmountReceived));

    /// <summary>
    /// 成交笔数：只算已结账(1)。
    ///
    /// 退款单(2)不算「成交」—— 它是一笔冲销，不是一次销售。
    /// 分子分母口径要能对上：
    ///   净营业额 = Σ已结账 + Σ退款(负数)
    ///   成交笔数 = 已结账笔数
    ///   笔单价   = 净营业额 / 成交笔数  →  「平均每单收多少钱」
    ///
    /// 如果把退款单也算进笔数，分母被撑大、分子还减了，
    /// 笔单价会被双重压低，老板会以为客单价掉了。
    /// 退款笔数单独用 CountRefundOrders 显示。
    /// </summary>
    public static int CountValidOrders(IEnumerable<ReportOrderDto> orders)
        => orders.Count(o => o.Status == 1);

    /// <summary>退款笔数。</summary>
    public static int CountRefundOrders(IEnumerable<ReportOrderDto> orders)
        => orders.Count(o => o.Status == 2);

    /// <summary>
    /// 同比：(本期 - 上期) / 上期。上期为 0 时返回 0（不抛异常）。
    /// 返回小数（如 0.082 表示 +8.2%）。
    /// </summary>
    public static decimal CalcGrowthRate(decimal current, decimal previous)
    {
        if (previous == 0) return 0m;
        return Round((current - previous) / Math.Abs(previous), 4);
    }

    /// <summary>
    /// 占比：part / total。分母为 0 时返回 0，不返回 NaN。
    /// </summary>
    public static decimal CalcShare(decimal part, decimal total)
    {
        if (total == 0) return 0m;
        return Round(part / total, 4);
    }

    /// <summary>
    /// 按小时分桶（0-23）。标注高峰时段（营业额最高的连续时段）。
    /// </summary>
    public static List<HourBucket> BucketByHour(IEnumerable<ReportOrderDto> orders)
    {
        var buckets = Enumerable.Range(0, 24)
            .Select(h => new HourBucket { Hour = h })
            .ToList();

        foreach (var o in orders)
        {
            if (o.IsVoid) continue;
            var hour = o.AttributedTime.Hour;
            buckets[hour].Turnover += o.AmountPay - o.AmountRefund;
            buckets[hour].OrderCount++;
        }

        foreach (var b in buckets)
            b.Turnover = Round(b.Turnover);

        // 计算条形图比例
        var maxForBar = buckets.Max(b => b.Turnover);
        foreach (var b in buckets)
            b.BarRatio = maxForBar > 0 ? (double)(b.Turnover / maxForBar) : 0;

        // 标注高峰：营业额最高的小时及其相邻（形成高峰时段）
        var maxTurnover = maxForBar;
        if (maxTurnover > 0)
        {
            var peakHour = buckets.First(b => b.Turnover == maxTurnover).Hour;
            // 高峰时段：前后各 1 小时内营业额 >= 50% 峰值的都算
            foreach (var b in buckets)
            {
                if (Math.Abs(b.Hour - peakHour) <= 1 && b.Turnover >= maxTurnover * 0.5m)
                    b.IsPeak = true;
            }
        }

        return buckets;
    }

    /// <summary>
    /// 菜品排行：按销售额（默认）或销量排序。
    /// 退菜（IsRefunded）不计入；赠菜（IsGift）计销量不计金额。
    /// </summary>
    public static List<RankItem> RankProducts(
        IEnumerable<ReportOrderDto> orders,
        bool byAmount = true,
        int topN = 20)
    {
        var map = new Dictionary<long, RankItem>();

        foreach (var o in orders)
        {
            if (o.IsVoid) continue;
            foreach (var item in o.Items)
            {
                if (item.IsRefunded) continue; // 退菜不计入
                if (!map.TryGetValue(item.ProductId, out var rank))
                {
                    rank = new RankItem { Id = item.ProductId, Name = item.ProductName };
                    map[item.ProductId] = rank;
                }
                rank.Quantity += item.Qty;
                if (!item.IsGift)
                    rank.Amount += item.Amount; // 赠菜不计金额
            }
        }

        var totalAmount = map.Values.Sum(r => r.Amount);
        var sorted = (byAmount
            ? map.Values.OrderByDescending(r => r.Amount)
            : map.Values.OrderByDescending(r => r.Quantity))
            .Take(topN)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
            sorted[i].Amount = Round(sorted[i].Amount);
            sorted[i].Share = CalcShare(sorted[i].Amount, totalAmount);
        }

        return sorted;
    }

    /// <summary>
    /// 桌台统计：桌次/金额/平均客单价。
    /// </summary>
    public static List<RankItem> RankDesks(IEnumerable<ReportOrderDto> orders)
    {
        var map = new Dictionary<long, RankItem>();

        foreach (var o in orders)
        {
            if (o.IsVoid || o.DeskId == 0) continue; // 快餐无桌台
            if (!map.TryGetValue(o.DeskId, out var rank))
            {
                rank = new RankItem { Id = o.DeskId, Name = o.DeskName };
                map[o.DeskId] = rank;
            }
            rank.Quantity += 1; // 桌次
            rank.Amount += o.AmountPay - o.AmountRefund;
        }

        var sorted = map.Values.OrderByDescending(r => r.Amount).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
            sorted[i].Amount = Round(sorted[i].Amount);
        }

        return sorted;
    }

    /// <summary>
    /// 桌台平均客单价：金额 / 桌次。桌次为 0 时返回 0。
    /// </summary>
    public static decimal CalcDeskAvg(RankItem desk)
        => CalcAvgTicket(desk.Amount, (int)desk.Quantity);

    /// <summary>
    /// 支付方式分布：按 PayType 汇总金额及占比。
    /// </summary>
    public static List<(int PayType, string Name, decimal Amount, decimal Share)> PayTypeBreakdown(
        IEnumerable<ReportOrderDto> orders)
    {
        var map = new Dictionary<int, (string Name, decimal Amount)>();

        foreach (var o in orders)
        {
            if (o.IsVoid) continue;
            foreach (var p in o.Payments)
            {
                if (!map.TryGetValue(p.PayType, out var entry))
                    entry = (p.PayTypeName, 0m);
                map[p.PayType] = (entry.Name, entry.Amount + p.Amount);
            }
        }

        var total = map.Values.Sum(e => e.Amount);
        return map
            .Select(kv => (kv.Key, kv.Value.Name, Round(kv.Value.Amount), CalcShare(kv.Value.Amount, total)))
            .OrderByDescending(x => x.Item3)
            .ToList();
    }

    /// <summary>
    /// 按日期范围筛选订单（归属时间在范围内）。
    /// </summary>
    public static List<ReportOrderDto> FilterByDate(
        IEnumerable<ReportOrderDto> orders,
        DateTime start,
        DateTime end)
    {
        return orders
            .Where(o => o.AttributedTime >= start && o.AttributedTime < end)
            .ToList();
    }

    private static decimal Round(decimal value, int decimals = 2)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
