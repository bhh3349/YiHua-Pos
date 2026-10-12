using YiHua.Pos.UI.Services.Payment;

namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 折扣 ViewModel（仅店长）。
/// </summary>
public class DiscountViewModel
{
    public DiscountType SelectedType { get; set; } = DiscountType.Rate;
    public decimal RateValue { get; set; } = 0.9m; // 9折
    public decimal FixedValue { get; set; }
    public string Reason { get; set; } = string.Empty;

    public long OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public bool IsManager { get; set; }

    /// <summary>构建折扣请求（可测试）。</summary>
    public DiscountRequest BuildRequest() => new()
    {
        Type = SelectedType,
        Value = SelectedType == DiscountType.Rate ? RateValue : FixedValue,
        OperatorId = OperatorId,
        OperatorName = OperatorName,
        IsManager = IsManager,
        Reason = Reason,
    };

    /// <summary>校验（可测试）。</summary>
    public string? Validate()
    {
        if (!IsManager)
            return "折扣/抹零仅店长可操作";
        if (SelectedType == DiscountType.Rate && (RateValue <= 0 || RateValue > 1))
            return "折扣率必须在 0~1 之间";
        if (SelectedType == DiscountType.Fixed && FixedValue < 0)
            return "减免金额不能为负";
        return null;
    }
}

/// <summary>
/// 客户选择 ViewModel。
/// </summary>
public class CustomerSelectViewModel
{
    public List<CustomerDto> Customers { get; } = new();
    public string SearchKeyword { get; set; } = string.Empty;
    public CustomerDto? Selected { get; set; }

    public bool IsLoading { get; private set; }

    /// <summary>搜索客户（可测试）。</summary>
    public List<CustomerDto> Search()
    {
        var kw = SearchKeyword?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(kw))
            return Customers.ToList();
        return Customers.Where(c =>
            c.Name.ToLowerInvariant().Contains(kw) ||
            (c.Phone?.Contains(kw) ?? false)).ToList();
    }

    /// <summary>新建临时客户（可测试）。</summary>
    public CustomerDto CreateTemporary(string name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("客户名称不能为空", nameof(name));
        var customer = new CustomerDto
        {
            Name = name.Trim(),
            Phone = phone?.Trim(),
            IsTemporary = true,
        };
        Customers.Add(customer);
        return customer;
    }
}
