namespace YiHua.Pos.UI.Services.Payment;

/// <summary>
/// 钱箱接口（UI 层契约，App 层用 T-106 的 ICashDrawer 实现）。
/// </summary>
public interface ICashDrawerOpener
{
    Task OpenAsync(CancellationToken ct = default);
}

/// <summary>
/// 客显接口（UI 层契约，App 层用 T-106 的 ICustomerDisplay 实现）。
/// </summary>
public interface ICustomerDisplayWriter
{
    Task ShowAmountsAsync(decimal total, decimal received, decimal change, CancellationToken ct = default);
    Task ShowWelcomeAsync(CancellationToken ct = default);
}

/// <summary>
/// 小票打印接口（UI 层契约，App 层用 T-107 模板实现）。
/// </summary>
public interface IReceiptPrinter
{
    /// <summary>打印小票。返回成功/失败（失败不回滚结账）。</summary>
    Task<bool> PrintAsync(ReceiptData data, CancellationToken ct = default);
}

/// <summary>小票数据。</summary>
public class ReceiptData
{
    public string OrderNo { get; set; } = string.Empty;
    public string DeskInfo { get; set; } = string.Empty;
    public List<ReceiptItem> Items { get; } = new();
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountLabel { get; set; }
    public decimal PayableAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string PayMethodName { get; set; } = string.Empty;
    public DateTime PayTime { get; set; } = DateTime.Now;
}

public class ReceiptItem
{
    public string Name { get; set; } = string.Empty;
    public string SpecText { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// 支付后编排：打印/客显/钱箱。
/// 打印失败不回滚结账，返回失败状态由 UI 提供重试入口。
/// </summary>
public class PaymentPrinter
{
    private readonly IReceiptPrinter _printer;
    private readonly ICustomerDisplayWriter _display;
    private readonly ICashDrawerOpener _drawer;

    public PaymentPrinter(IReceiptPrinter printer, ICustomerDisplayWriter display, ICashDrawerOpener drawer)
    {
        _printer = printer;
        _display = display;
        _drawer = drawer;
    }

    /// <summary>
    /// 结账后执行：现金开钱箱 → 客显 → 打印。
    /// </summary>
    /// <param name="isCash">是否为现金支付（仅现金开钱箱）</param>
    /// <returns>打印是否成功（失败不抛异常，由 UI 重试）</returns>
    public async Task<bool> AfterPaymentAsync(ReceiptData data, bool isCash, CancellationToken ct = default)
    {
        if (isCash)
        {
            try { await _drawer.OpenAsync(ct); } catch { /* 钱箱失败不阻塞 */ }
        }

        try
        {
            await _display.ShowAmountsAsync(data.TotalAmount, data.ReceivedAmount, data.ChangeAmount, ct);
        }
        catch { /* 客显失败不阻塞 */ }

        // 打印失败返回 false，不抛异常，不回滚结账
        try
        {
            return await _printer.PrintAsync(data, ct);
        }
        catch
        {
            return false;
        }
    }
}
