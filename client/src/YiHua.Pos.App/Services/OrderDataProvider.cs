using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Enums;
using YiHua.Pos.Domain.Interfaces;
using YiHua.Pos.UI.Services.Report;

namespace YiHua.Pos.App.Services;

/// <summary>
/// 订单数据提供者：把 Domain 的 IOrderRepository 桥接到报表层契约。
///
/// 与 ProductDataSource 同构 —— UI 项目被冻结不能引用 Domain，
/// 报表层自己定了 IOrderDataProvider，注释写明「App 层用 IOrderRepository 实现」。
///
/// 取数方式说明：IOrderRepository 没有 GetAllAsync，最接近的是
/// GetByDateRangeAsync。这里取一个足够宽的时间窗取全量 —— 单店 POS
/// 不会有几年前的订单，报表本来也只按日期查。
///
/// 注意 Order.Items / Order.Payments 是聚合导航属性，
/// OrderRepository.GetByIdAsync 会一并加载（见 Domain 约定），
/// 所以必须走 GetByIdAsync，不能只 GetByDateRangeAsync，否则明细和收款是空的。
/// </summary>
internal sealed class OrderDataProvider : IOrderDataProvider
{
    private readonly IOrderRepository _orders;

    public OrderDataProvider(IOrderRepository orders) => _orders = orders;

    /// <summary>全量订单的时间窗下界：往前推 10 年。</summary>
    private static readonly DateTime Since = DateTime.Now.AddYears(-10);

    public async Task<List<ReportOrderDto>> GetAllAsync(CancellationToken ct = default)
    {
        var summaries = await _orders
            .GetByDateRangeAsync(Since, DateTime.Now.AddDays(1), ct)
            .ConfigureAwait(false);

        var result = new List<ReportOrderDto>(summaries.Count);
        foreach (var o in summaries)
        {
            // 逐单取全量（带 Items / Payments）
            var full = await _orders.GetByIdAsync(o.Id, ct).ConfigureAwait(false);
            result.Add(Map(full ?? o));
        }

        return result;
    }

    /// <summary>Domain.Order → ReportOrderDto。字段一一对应。</summary>
    internal static ReportOrderDto Map(Order o)
    {
        var dto = new ReportOrderDto
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            DeskId = o.DeskId,
            OrderType = (int)o.OrderType,
            Status = (int)o.Status,
            AmountTotal = o.AmountTotal,
            AmountDiscount = o.AmountDiscount,
            AmountPay = o.AmountPay,
            AmountRefund = o.AmountRefund,
            AmountReceived = o.AmountReceived,
            AmountChange = o.AmountChange,
            CreatedAt = o.CreatedAt,
            SettleTime = o.SettleTime,
        };

        // Domain.Order 没有 DeskName 字段（桌台名在 Desk 表）。
        // 报表按桌台排行时用桌号兜底；要显示中文桌名需关联 Desk 仓储，
        // 属于报表增强项，此处不猜。
        dto.DeskName = o.DeskId > 0 ? $"桌 {o.DeskId}" : "";

        foreach (var it in o.Items)
        {
            dto.Items.Add(new ReportOrderItemDto
            {
                ProductId = it.ProductId,
                ProductName = it.ProductName,
                Qty = it.Qty,
                Price = it.Price,
                Amount = it.Amount,
            });
        }

        foreach (var pay in o.Payments)
        {
            dto.Payments.Add(new ReportPaymentDto
            {
                PayType = (int)pay.PayType,
                // 用订单上存的中文名（「现金」「挂账」…）。
                // 直接 ToString() 会把枚举名 "Cash" 报给老板看 ——
                // 报表上出现英文枚举名，一看就是没做完。
                PayTypeName = string.IsNullOrWhiteSpace(pay.PayTypeName)
                    ? DescribePayType(pay.PayType)
                    : pay.PayTypeName,
                Amount = pay.Amount,
            });
        }

        return dto;
    }

    /// <summary>支付方式中文名（订单上没存名字时的兜底）。</summary>
    private static string DescribePayType(PayType type) => type switch
    {
        PayType.Cash => "现金",
        PayType.Custom => "自定义收款",
        PayType.Member => "会员卡",
        PayType.Bank => "银行卡",
        PayType.QrCode => "扫码支付",
        _ => "其他",
    };
}